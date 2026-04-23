#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 2 ]]; then
  echo "Usage: sudo bash ops/systemd/install-service-envfile-dropin.sh <service-name> <env-file-path>" >&2
  exit 1
fi

if [[ "${EUID}" -ne 0 ]]; then
  echo "Run this script as root." >&2
  exit 1
fi

service_name="$1"
env_file_path="$2"
dropin_dir="/etc/systemd/system/${service_name}.service.d"
dropin_file="${dropin_dir}/override.conf"

install -d -m 0755 "${dropin_dir}"

cat > "${dropin_file}" <<EOF
[Service]
EnvironmentFile=${env_file_path}
EOF

chmod 0644 "${dropin_file}"
systemctl daemon-reload

echo "Installed ${dropin_file} (replaced with the repository-supported minimal EnvironmentFile drop-in)"
systemctl cat "${service_name}"
