#!/usr/bin/env bash
# Export icon.xcf, the icon's source, to icon.png (256x256, the size Thunderstore wants) with GIMP.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
gimp-console -i --batch-interpreter=plug-in-script-fu-eval -b '
  (let* ((image (car (gimp-file-load RUN-NONINTERACTIVE "icon.xcf"))))
    (gimp-image-flatten image)
    (gimp-file-save RUN-NONINTERACTIVE image "icon.png" -1))' --quit
