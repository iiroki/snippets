#!/bin/bash

#
# IoT Edge Debian install script
#
# Source:
# https://learn.microsoft.com/en-us/azure/iot-edge/how-to-provision-single-device-linux-symmetric#install-iot-edge
#
# NOTE: This script is still to be tested!
#

OS_VERSION=12

echo "Installing IoT Edge for Debian (Version: $OS_VERSION)..."

# Source: https://gist.github.com/mihow/9c7f559807069a03e302605691f85572
echo "Loading secrets from .env"
if [ ! -f .env ]
then
  echo ".env not found"
  exit 1
fi

set -a && source .env && set +a
if [ -z "$IOTEDGE_DEVICE_CONNECTION_STRING" ]
then
  echo "IOTEDGE_DEVICE_CONNECTION_STRING is not defined"
  exit 1
fi

echo "Found IoT Edge device connection string - Length: ${#IOTEDGE_DEVICE_CONNECTION_STRING}"

# Install Microsoft packages
echo
echo "Installing Microsoft packages..."
echo
rm ./packages-microsoft-prod.deb
curl https://packages.microsoft.com/config/debian/$OS_VERSION/packages-microsoft-prod.deb \
  > ./packages-microsoft-prod.deb

apt install ./packages-microsoft-prod.deb

echo
echo "Installing Moby engine..."
echo
apt-get update; \
  apt-get install moby-engine --yes

echo
echo "Installing the IoT Edge runtime..."
echo
apt-get update; \
  apt-get install aziot-edge --yes

# Configure Docker logging
# TODO

systemctl restart docker

# Provision the IoT Edge device with the device connection string
echo
echo "Provisioning the IoT Edge device..."
echo
iotedge config mp --connection-string "$IOTEDGE_DEVICE_CONNECTION_STRING"

# Apply the IoT Edge configuration
echo
echo "Applying the IoT Edge configuration..."
echo
iotedge config apply

# Validate the installation
echo
echo "Validating IoT Edge installation..."
echo
iotedge --version
