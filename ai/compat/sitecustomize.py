"""Local Python startup hooks for ML-Agents training compatibility."""

from __future__ import annotations

from functools import wraps


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


_force_legacy_onnx_exporter()
