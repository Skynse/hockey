# Hockey RL Training

## Quick Start

1. Open Unity project and load `Assets/Scenes/gym.unity`.
2. Wait for compilation to finish and make sure there are no C# errors.
3. Start training from the project root:

```bash
./ai/train_gym_defense.sh gym_defense_v2
```

This script:
- Works from any current directory.
- Auto-selects a compatible Python stack for `mlagents==1.1.0`.
- Writes outputs under `ai/results/<run-id>/`.

## Resume a Run

```bash
./ai/train_gym_defense.sh gym_defense_v2 --resume
```

## Force New Run ID Reuse

```bash
./ai/train_gym_defense.sh gym_defense_v2 --force
```

## Notes

- Expected Python package versions:
  - `mlagents==1.1.0`
  - `mlagents_envs==1.1.0`
  - `onnx==1.15.0`
  - `protobuf==3.20.3`
- If your local `ai/venv` is incompatible, the launcher falls back to other installed Python environments.
