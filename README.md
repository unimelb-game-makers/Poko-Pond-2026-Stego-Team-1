# core-project
A template core documentation structure for a UMGMC Project.

# core-project
A template core documentation structure for a UMGMC Project.


## CI-CD
# Documentation
### How to use the CI/CD workflow

- Triggers: runs on pull requests to `main`, pushes to `main`, or manually via **Actions → CI/CD → Run workflow**.
- Build targets: Windows (always), plus Linux and macOS after a successful Windows build on push.
- Artifacts: builds for each platform are uploaded to the workflow run.

### Required GitHub secrets (Repository → Settings → Secrets and variables → Actions → New repository secret)

- `UNITY_LICENSE`: Full Unity license file content
- `UNITY_EMAIL`: Unity account email
- `UNITY_PASSWORD`: Unity account password
- `STEAM_USERNAME`: Steam account username
- `STEAM_CONFIG_VDF`: Steam `config.vdf` file content

### Required GitHub variables (Repository → Settings → Secrets and variables → Actions → New repository variable)

- `UNITY_VERSION`: Unity version string (e.g., 2023.2.9f1)
- `STEAM_APP_ID`: Steam App ID (e.g., 123456)

### Manual run steps

1) Ensure all secrets and variables above are set.
2) Go to GitHub **Actions → CI/CD → Run workflow**.
3) Confirm the branch (defaults to `main`) and click **Run workflow**.
4) Download build artifacts from the run summary once it finishes. Deploy to Steam happens automatically on push builds after Windows/Linux/macOS artifacts are produced.


## Documentation

## [Operations](./documentation/operations/)

The file [team.md](./documentation/operations/team.md) contains the team member names and roles for this project.

The file [credits.md](./documentation/operations/credits.md) credits all the individuals and organisations who contributed to this project.

## [Creative](./documentation/creative/)

The file [art.md](./documentation/creative/art.md) contains the art pipeline for the project

The file [animation.md](./documentation/creative/animation.md) contains the animation pipeline for the project

The file [music.md](./documentation/creative/music.md) contains the music pipeline for the project

## [Technical](./documentation/technical/)

The file [godot.md](./documentation/technical/godot.md) contains the Godot best practices and guidelines for the project.

The file [unity.md](./documentation/technical/unity.md) contains the Unity best practices and guidelines for the project.
