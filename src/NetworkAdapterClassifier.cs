using System;
using System.Net.NetworkInformation;

namespace TrafficView
{
    internal static class NetworkAdapterClassifier
    {
        internal static bool IsSelectable(NetworkInterface networkInterface)
        {
            if (networkInterface == null)
            {
                return false;
            }

            NetworkInterfaceType type;
            try
            {
                type = networkInterface.NetworkInterfaceType;
            }
            catch (NetworkInformationException)
            {
                return false;
            }

            if (type == NetworkInterfaceType.Loopback ||
                type == NetworkInterfaceType.Tunnel ||
                type == NetworkInterfaceType.Unknown)
            {
                return false;
            }

            if (LooksLikeAuxiliaryVirtualAdapter(networkInterface))
            {
                return false;
            }

            try
            {
                if (networkInterface.IsReceiveOnly)
                {
                    return false;
                }
            }
            catch (NetworkInformationException)
            {
                return false;
            }

            return SupportsTrafficProtocols(networkInterface);
        }
        internal static bool IsSelectableInSetup(NetworkInterface networkInterface)
        {
            return IsSelectable(networkInterface) &&
                !LooksLikeVirtualAdapterForSetup(networkInterface);
        }
        internal static bool LooksLikeAuxiliaryVirtualAdapter(NetworkInterface networkInterface)
        {
            string name = SafeInterfaceText(networkInterface != null ? networkInterface.Name : null);
            string description = SafeInterfaceText(networkInterface != null ? networkInterface.Description : null);

            return ContainsAuxiliaryInterfaceMarker(name) ||
                ContainsAuxiliaryInterfaceMarker(description);
        }
        internal static bool LooksLikeVirtualAdapterForSetup(NetworkInterface networkInterface)
        {
            string name = SafeInterfaceText(networkInterface != null ? networkInterface.Name : null);
            string description = SafeInterfaceText(networkInterface != null ? networkInterface.Description : null);

            return ContainsVirtualAdapterMarker(name) ||
                ContainsVirtualAdapterMarker(description);
        }
        internal static string SafeInterfaceText(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().ToLowerInvariant();
        }
        internal static bool ContainsAuxiliaryInterfaceMarker(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            return value.Contains("ndis 6 filter") ||
                value.Contains("lightweight filter") ||
                value.Contains("filter driver") ||
                value.Contains("qos packet scheduler") ||
                value.Contains("kerneldebugger") ||
                value.Contains("kernel debugger") ||
                value.Contains("pseudo-interface") ||
                value.Contains("wi-fi direct") ||
                value.Contains("wifi direct") ||
                value.Contains("virtual wifi");
        }
        internal static bool ContainsVirtualAdapterMarker(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            return value.Contains("virtual") ||
                value.Contains("vpn") ||
                value.Contains("tunnel") ||
                value.Contains("tap-") ||
                value.Contains("tap ") ||
                value.Contains("tap-windows") ||
                value.Contains("wireguard") ||
                value.Contains("mullvad") ||
                value.Contains("openvpn") ||
                value.Contains("hyper-v") ||
                value.Contains("vethernet") ||
                value.Contains("wi-fi direct") ||
                value.Contains("wifi direct") ||
                value.Contains("pseudo-interface") ||
                value.Contains("pseudo interface");
        }
        internal static bool IsCapturable(NetworkInterface networkInterface)
        {
            OperationalStatus operationalStatus;
            return IsSelectable(networkInterface) &&
                TryGetOperationalStatus(networkInterface, out operationalStatus) &&
                operationalStatus == OperationalStatus.Up &&
                HasUsableUnicastAddress(networkInterface);
        }
        internal static int GetAutomaticAdapterTypePriority(NetworkInterface networkInterface)
        {
            if (networkInterface == null)
            {
                return 0;
            }

            NetworkInterfaceType type;
            try
            {
                type = networkInterface.NetworkInterfaceType;
            }
            catch (NetworkInformationException)
            {
                return 0;
            }

            switch (type)
            {
                case NetworkInterfaceType.Ethernet:
                case NetworkInterfaceType.GigabitEthernet:
                case NetworkInterfaceType.FastEthernetFx:
                case NetworkInterfaceType.FastEthernetT:
                    return 300;
                case NetworkInterfaceType.Wireless80211:
                    return 250;
                case NetworkInterfaceType.Ppp:
                    return 200;
                default:
                    return 100;
            }
        }
        internal static bool TryGetInterfaceSpeed(NetworkInterface networkInterface, out long speed)
        {
            speed = 0L;

            if (networkInterface == null)
            {
                return false;
            }

            try
            {
                speed = Math.Max(0L, networkInterface.Speed);
                return true;
            }
            catch (NetworkInformationException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }
        internal static bool SupportsTrafficProtocols(NetworkInterface networkInterface)
        {
            try
            {
                return networkInterface.Supports(NetworkInterfaceComponent.IPv4) ||
                    networkInterface.Supports(NetworkInterfaceComponent.IPv6);
            }
            catch (NetworkInformationException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }
        internal static bool TryGetOperationalStatus(NetworkInterface networkInterface, out OperationalStatus operationalStatus)
        {
            operationalStatus = OperationalStatus.Unknown;

            if (networkInterface == null)
            {
                return false;
            }

            try
            {
                operationalStatus = networkInterface.OperationalStatus;
                return true;
            }
            catch (NetworkInformationException)
            {
                return false;
            }
        }
        internal static bool HasUsableUnicastAddress(NetworkInterface networkInterface)
        {
            IPInterfaceProperties properties;
            if (!TryGetIpProperties(networkInterface, out properties) || properties == null)
            {
                return false;
            }

            foreach (UnicastIPAddressInformation addressInformation in properties.UnicastAddresses)
            {
                if (addressInformation == null || addressInformation.Address == null)
                {
                    continue;
                }

                if (System.Net.IPAddress.IsLoopback(addressInformation.Address))
                {
                    continue;
                }

                if (addressInformation.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ||
                    addressInformation.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                {
                    return true;
                }
            }

            return false;
        }
        internal static bool TryGetIpProperties(NetworkInterface networkInterface, out IPInterfaceProperties properties)
        {
            properties = null;

            if (networkInterface == null)
            {
                return false;
            }

            try
            {
                properties = networkInterface.GetIPProperties();
                return properties != null;
            }
            catch (NetworkInformationException)
            {
                return false;
            }
        }
    }
}
