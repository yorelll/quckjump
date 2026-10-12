# QuickJump Project Rules

## C# development and verification

- This local environment has **no C# compiler, .NET SDK, Visual Studio, or MSBuild**. Do not claim that C# code compiled locally.
- Every C# implementation subagent must add or update detailed, focused test cases for every behavior they change.
- C# compilation and test validation must happen through the repository's **GitHub Actions CI**.
- Before modifying shared integration code, an implementation subagent must work on its own branch/worktree and push that branch to GitHub.
- The subagent must use `gh` to monitor the matching CI run and report its actual conclusion and relevant logs. A successful CI run is required before integration.
- A failed CI run must be diagnosed and fixed through new commits; do not use `--no-verify`, skip tests, or claim success without a green GitHub Action.
- The main agent must delegate implementation to an implementation subagent and code review to a code-review subagent. After an implementation agent reports completion, a review agent reviews the CI-validated commit. If review identifies a confirmed issue, resume the **same** implementation agent to fix it, push it, wait for CI again, and resume the **same** review agent for re-review. Continue until the review agent has no remaining confirmed findings. Do not start a new implementation/review agent unless the existing agent's context is exhausted or it cannot be resumed.

## Testing

- Each feature requires detailed automated test cases covering the happy path, validation failures, boundary cases, persistence behavior, and query/filtering behavior as applicable.
- Test cases must be committed and run by GitHub Actions before a release.

## Release discipline

- Do not create or replace a Release until implementation tests, GitHub CI, and reviewer re-verification have all completed with no remaining confirmed findings.
- A public tag/release must point at the reviewed, CI-validated commit.
- Releases use normal semantic-version tags: `v<semver>` (for example `v1.0.0`).
- The user-facing GitHub release and ZIP archive use the product name: `QuickJump v<semver>` and `QuickJump-<semver>.zip`.
- The release workflow must package the Flow Launcher plugin manifest, executable DLL, managed dependencies, and plugin icon at the ZIP root. The resulting archive must be directly extractable into a Flow Launcher plugin directory.
- Release workflow structure and staging validation should follow the reference implementation in `D:\work\tools\ai-ask` on its `feature/csharp-native` branch, adapted for this repository and plugin layout.
