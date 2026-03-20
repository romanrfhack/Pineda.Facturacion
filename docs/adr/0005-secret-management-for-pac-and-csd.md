# Title
Secret Management For PAC And CSD

## Status
Accepted

## Context
PAC credentials and CSD materials are sensitive and must not be embedded in source-controlled code or documentation.

## Decision
Externalize all PAC and CSD secrets. Documentation and configuration examples may reference placeholder names only. Certificates, keys, passwords, API keys, and equivalent sensitive values must be resolved from external configuration or secret-management infrastructure at runtime.

## Consequences
- No real secrets appear in repository docs or code.
- Environments can supply different secure values without code changes.
- Adapter implementations must support external secret resolution.
- Documentation remains safe to share and review.
