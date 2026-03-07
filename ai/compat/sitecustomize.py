"""Local Python startup hooks for ML-Agents training compatibility."""

from __future__ import annotations

from functools import wraps
from typing import Tuple


def _to_version_tuple(value: object) -> Tuple[int, int, int] | None:
    """
    Best-effort conversion for communication-version values.

    ML-Agents 1.1.0 uses distutils.StrictVersion and then accesses `.version`,
    but some runtime paths can hand back empty/odd version objects.
    """
    if value is None:
        return None

    # distutils StrictVersion normally sets `.version`.
    version_attr = getattr(value, "version", None)
    if isinstance(version_attr, tuple) and version_attr:
        ints = [int(x) for x in version_attr[:3]]
        while len(ints) < 3:
            ints.append(0)
        return tuple(ints)  # type: ignore[return-value]

    # packaging.version.Version exposes `.release`.
    release = getattr(value, "release", None)
    if isinstance(release, tuple) and release:
        ints = [int(x) for x in release[:3]]
        while len(ints) < 3:
            ints.append(0)
        return tuple(ints)  # type: ignore[return-value]

    # Fall back to parsing from string.
    try:
        text = str(value).strip()
    except Exception:
        return None
    if not text:
        return None

    parts: list[int] = []
    for raw in text.split("."):
        digits = "".join(ch for ch in raw if ch.isdigit())
        if not digits:
            break
        parts.append(int(digits))
        if len(parts) == 3:
            break

    if not parts:
        return None

    while len(parts) < 3:
        parts.append(0)
    return tuple(parts)  # type: ignore[return-value]


def _force_legacy_onnx_exporter() -> None:
    # Torch 2.9+ defaults ONNX export to dynamo=True, which requires onnxscript.
    # ML-Agents 1.1.0 expects ONNX 1.15.0/protobuf 3.20.x, so we force legacy export.
    try:
        import torch.onnx  # type: ignore
    except Exception:
        return

    original_export = torch.onnx.export

    @wraps(original_export)
    def export_with_legacy_default(*args, **kwargs):
        kwargs.setdefault("dynamo", False)
        return original_export(*args, **kwargs)

    torch.onnx.export = export_with_legacy_default


def _patch_mlagents_version_compat() -> None:
    try:
        from mlagents_envs.environment import UnityEnvironment
    except Exception:
        return

    def patched_check_communication_compatibility(
        unity_com_ver: str, python_api_version: str, unity_package_version: str
    ) -> bool:
        unity_version = _to_version_tuple(unity_com_ver)
        api_version = _to_version_tuple(python_api_version)
        if unity_version is None or api_version is None:
            return False

        if unity_version[0] == 0:
            if unity_version[0] != api_version[0] or unity_version[1] != api_version[1]:
                return False
        elif unity_version[0] != api_version[0]:
            return False
        else:
            # Keep existing behavior: allow minor mismatches when major matches.
            pass
        return True

    UnityEnvironment._check_communication_compatibility = staticmethod(  # type: ignore[attr-defined]
        patched_check_communication_compatibility
    )


_force_legacy_onnx_exporter()
_patch_mlagents_version_compat()
