# PipelineMonitor Spec #001: Repo Configuration

- JSON file in $repoRoot/.pipelines/config.json
- OR $repoRoot/.pipelines.json
- Maps files in repo to pipelines on Azure DevOps.

```jsonc
{
    "pipelines": [
        {
            "name": "example-pipeline",
            "filePath": "src/example/**",
            "defaultBranch": "main",
            "azureDevOps": {
                "organization": "my-org",
                "project": "my-project",
                "pipelineId": 42
            },
            "tags": [
                "daily"
            ]
        }
    ]
}
```
