# Deployment Guide

YOLO uses two long-lived Azure environments. Pull requests run CI only and never receive Azure credentials or create Azure resources.

| Environment | Source branch | Function App | Hyperliquid | Deployment |
| --- | --- | --- | --- | --- |
| Development | `develop` | `yolo-funk-dev` | Testnet | Automatic after merge |
| Production | `master` | `yolo-funk-prod` | Mainnet | Starts after release merge; requires approval |

## GitHub setup

Create GitHub environments named `development` and `production`. Configure each with environment-scoped values so credentials and resource names cannot cross environments:

- Secret: `AZURE_CREDENTIALS`
- Variables: `AZURE_RESOURCE_GROUP`, `AZURE_LOCATION`, `AZURE_KEYVAULT_NAME`
- Production-only optional variable: `ALERT_EMAIL`

Add a required reviewer to `production`. Do not add a deployment approval to `development`.

Create `develop` from `master` and make it the default branch. Protect both branches:

- Require the `.NET / build` check and at least one approving review.
- Prevent direct pushes and force pushes.
- Target normal and Dependabot pull requests at `develop`.
- Use PRs from `develop` to `master` as production releases.
- Merge release PRs with a merge commit; the production workflow rejects direct, squash, and rebase commits on `master`.

The deployment identity for each environment needs Contributor on only its resource group and User Access Administrator on that resource group (or its Key Vault). The latter allows the workflow to grant `Key Vault Secrets User` to that environment's Function App identity.

## Normal deployment flow

1. Open a PR against `develop`. CI builds and runs tests excluding the `Integration` category; no Azure deployment occurs.
2. Merge the PR. The exact resulting `develop` SHA deploys automatically to the development resource group and `yolo-funk-dev`.
3. Observe testnet execution, Key Vault resolution, timers, storage, and telemetry.
4. Open a release PR from `develop` to `master`.
5. Merge the release PR. The production job waits at the protected `production` environment before any checkout, infrastructure change, or code deployment occurs.
6. Approve the deployment. The workflow checks out and verifies the exact merge SHA, then deploys it to `yolo-funk-prod`.

Manual workflow dispatch is retained for redeployment. Select the workflow from `develop` when deploying `dev`, or from `master` when deploying `prod`; mismatched branch/environment combinations fail before Azure login. Production dispatches still require approval.

## Azure layout

Development and production use separate resource groups in the same subscription. Each resource group contains its own Function App, Consumption plan, storage account, managed identity, Key Vault, Log Analytics workspace, and Application Insights instance.

The Bicep templates create infrastructure, including an empty RBAC-enabled Key Vault. Secret values must be populated separately before the first application deployment; see [Azure Key Vault Secrets Setup](AZURE-KEY-VAULT-SECRETS-SETUP.md).

Globally scoped names must be unique. In particular, choose distinct `AZURE_KEYVAULT_NAME` values. The Function App names are fixed as `yolo-funk-dev` and `yolo-funk-prod`, while storage names are deterministically unique per resource group.

## Migration order

1. Create and configure the `development` GitHub environment and its dedicated Azure identity, resource group name, location, and globally unique Key Vault name.
2. Provision the development Key Vault, populate testnet-only secrets, and deploy `develop`.
3. Confirm the development managed identity has access only to its own vault and validate testnet behavior and telemetry.
4. Create the `production` GitHub environment. Point it at the existing production resource group initially so the current Function App is updated in place, not recreated.
5. Create/populate the production-only vault with the environment-neutral secret names. Do not remove the legacy secrets until the new references resolve successfully.
6. Merge a release PR and approve production. Verify the deployed SHA and live health before retiring the legacy shared-vault configuration.

## Rollback

Production always deploys a commit on `master`. To roll back, revert the problematic release commit (or create a PR restoring the known-good state), merge it into `master`, and approve the resulting production deployment. This preserves an auditable history and prevents arbitrary unreviewed commits from reaching mainnet.

Leaving or rejecting a production approval does not modify Azure; the existing production deployment continues running.
