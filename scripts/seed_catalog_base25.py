"""Backfill local dev: sube las 25 imagenes base de la galeria ML a Blob (Azurite)
y siembra BandasCriminales/TatuajesCatalogo en SQL para que coincidan 1:1 con los
catalog_id (tat_00001..tat_00025) ya enrolados en models/gallery_dinov2.npz.

No usa CatalogService.EnrollAsync (eso llamaria a /enroll y crearia catalog_ids
NUEVOS, duplicando los que el modelo ya usa para matchear). Solo escribe los
metadatos que faltan para las identidades que ya existen en la galeria.

Requiere: pip install azure-storage-blob (ya esta en rtattooai-ml/venv)
Uso: python scripts/seed_catalog_base25.py
"""
from __future__ import annotations

import hashlib
import subprocess
import uuid
from pathlib import Path

import numpy as np
from azure.storage.blob import BlobServiceClient, ContentSettings

REPO_ROOT = Path(__file__).resolve().parents[1]
WORKSPACE_ROOT = REPO_ROOT.parent
GALLERY_NPZ = WORKSPACE_ROOT / "rtattooai-ml" / "models" / "gallery_dinov2.npz"
DESIGNS_DIR = WORKSPACE_ROOT / "rtattooai-ml" / "datasets" / "gang_tattoos"

BLOB_CONNECTION_STRING = "UseDevelopmentStorage=true"
BLOB_CONTAINER = "rtattooai"
SQL_SERVER_CONTAINER = "rtattooai-sqlserver"
SQL_SA_PASSWORD = "RTattooAI_Dev2026!"
REGISTRADO_POR_ID = "0085DB0E-D0D4-4621-B4C5-DCF3E7F633DF"  # analista@pnp.gob.pe (seed user)


def load_catalog_id_to_name() -> dict[str, str]:
    data = np.load(GALLERY_NPZ, allow_pickle=True)
    cids = [str(c) for c in data["catalog_ids"]]
    names = [str(n) for n in data["names"]]
    mapping: dict[str, str] = {}
    for cid, name in zip(cids, names):
        mapping.setdefault(cid, name)
    return mapping


def find_design_image(name: str) -> Path:
    matches = sorted(DESIGNS_DIR.glob(f"{name}-removebg-preview*.png"))
    if not matches:
        raise FileNotFoundError(f"No se encontro imagen para el diseño '{name}' en {DESIGNS_DIR}")
    return matches[0]


def sql_escape(value: str) -> str:
    return value.replace("'", "''")


def run_sql(statement: str) -> None:
    subprocess.run(
        [
            "docker", "exec", SQL_SERVER_CONTAINER,
            "/opt/mssql-tools18/bin/sqlcmd",
            "-S", "localhost", "-U", "sa", "-P", SQL_SA_PASSWORD, "-C",
            "-Q", statement,
        ],
        check=True,
    )


def main() -> None:
    mapping = load_catalog_id_to_name()
    base_ids = sorted(cid for cid in mapping if cid <= "tat_00025")
    print(f"Sembrando {len(base_ids)} identidades base: {base_ids[0]}..{base_ids[-1]}")

    # Azurite local no soporta las api_version mas nuevas del SDK; se fija una version vieja y estable.
    blob_service = BlobServiceClient.from_connection_string(BLOB_CONNECTION_STRING, api_version="2021-12-02")
    container = blob_service.get_container_client(BLOB_CONTAINER)
    try:
        container.create_container()
    except Exception:
        pass  # ya existe

    sql_statements: list[str] = []

    for catalog_id in base_ids:
        name = mapping[catalog_id]
        image_path = find_design_image(name)
        image_bytes = image_path.read_bytes()
        image_hash = hashlib.sha256(image_bytes).hexdigest()

        tatuaje_id = str(uuid.uuid4())
        banda_id = str(uuid.uuid4())
        blob_path = f"catalogo/{tatuaje_id}/original.png"

        container.upload_blob(
            name=blob_path,
            data=image_bytes,
            overwrite=True,
            content_settings=ContentSettings(content_type="image/png"),
        )

        band_name_escaped = sql_escape(name)
        nombre_escaped = sql_escape(name)
        blob_path_escaped = sql_escape(blob_path)

        sql_statements.append(
            f"INSERT INTO RTattooAI.dbo.BandasCriminales "
            f"(Id, Nombre, EstaActivo, FechaRegistro, RegistradoPorId) "
            f"VALUES ('{banda_id}', '{band_name_escaped}', 1, SYSUTCDATETIME(), '{REGISTRADO_POR_ID}');"
        )
        sql_statements.append(
            f"INSERT INTO RTattooAI.dbo.TatuajesCatalogo "
            f"(Id, Codigo, Nombre, BandaId, ImagenUrl, ImagenHash, Fuente, EstaActivo, Validado, "
            f"FechaRegistro, RegistradoPorId) "
            f"VALUES ('{tatuaje_id}', '{catalog_id}', '{nombre_escaped}', '{banda_id}', "
            f"'{blob_path_escaped}', '{image_hash}', 'semilla_galeria_base', 1, 1, "
            f"SYSUTCDATETIME(), '{REGISTRADO_POR_ID}');"
        )

        print(f"  {catalog_id} <- {name} ({image_path.name}) hash={image_hash[:12]}...")

    batch_sql = "\n".join(sql_statements)
    run_sql(batch_sql)
    print("Listo. Filas insertadas en BandasCriminales y TatuajesCatalogo.")


if __name__ == "__main__":
    main()
