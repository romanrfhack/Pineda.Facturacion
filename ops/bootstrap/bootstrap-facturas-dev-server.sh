#!/usr/bin/env bash
set -euo pipefail

if [[ "${EUID}" -ne 0 ]]; then
  echo "Run this script as root." >&2
  exit 1
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/../.." && pwd)"

service_name="facturas-dev-api"
env_dir="/etc/facturas-dev"
env_file="${env_dir}/facturas-dev-api.env"
secret_references_file="${env_dir}/facturas-dev-secretreferences.json"
publish_root="/var/www/facturas-dev-backend"
publish_path="${publish_root}/publish"
env_example="${repo_root}/ops/env/facturas-dev-api.env.example"
dropin_installer="${repo_root}/ops/systemd/install-service-envfile-dropin.sh"

install -d -m 0750 "${env_dir}"
install -d -o www-data -g www-data -m 0755 "${publish_root}"
install -d -o www-data -g www-data -m 0755 "${publish_path}"

if [[ ! -f "${env_file}" ]]; then
  install -m 0600 "${env_example}" "${env_file}"
  echo "Created ${env_file} from example. Replace all placeholder values before restarting the service."
else
  echo "Preserved existing ${env_file}."
fi

if [[ ! -f "${secret_references_file}" ]]; then
  cat > "${secret_references_file}" <<'EOF'
{
  "SecretReferences": {
    "Values": {
      "FACTURALOPLUS_API_KEY_REFERENCE": "",
      "CSD_CERTIFICATE_REFERENCE": "",
      "CSD_PRIVATE_KEY_REFERENCE": "",
      "CSD_PRIVATE_KEY_PASSWORD_REFERENCE": ""
    }
  }
}
EOF
  chmod 600 "${secret_references_file}"
  echo "Created ${secret_references_file}. Fill it with real SecretReferences values before PAC/CSD operations."
else
  echo "Preserved existing ${secret_references_file}."
fi

bash "${dropin_installer}" "${service_name}" "${env_file}"

cat <<EOF
Next steps:
1. Edit ${env_file} and replace every placeholder with real values.
2. Edit ${secret_references_file} and set real SecretReferences values for PAC/CSD material.
3. Verify systemd is reading the EnvironmentFile:
   systemctl cat ${service_name}
4. Restart and validate:
   systemctl restart ${service_name}
   systemctl status ${service_name} --no-pager -l
   curl -fsS http://localhost:5007/health/live
   curl -fsS http://localhost:5007/health/ready
EOF
