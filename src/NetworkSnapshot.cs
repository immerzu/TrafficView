using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace TrafficView
{
    internal sealed class AdapterListItem
    {
        public AdapterListItem(string id, string name, string displayText, bool isAvailable = true)
        {
            this.Id = id ?? string.Empty;
            this.Name = name ?? string.Empty;
            this.DisplayText = displayText ?? string.Empty;
            this.IsAvailable = isAvailable;
        }

        public string Id { get; private set; }

        public string Name { get; private set; }

        public string DisplayText { get; private set; }

        public bool IsAvailable { get; private set; }

        public override string ToString()
        {
            return this.DisplayText;
        }
    }

    internal struct NetworkSnapshot
    {
        private const int IfMaxStringSize = 256;
        private const int IfMaxPhysAddressLength = 32;
        private const uint NoError = 0U;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MibIfRow2
        {
            public ulong InterfaceLuid;
            public uint InterfaceIndex;
            public Guid InterfaceGuid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = IfMaxStringSize + 1)]
            public string Alias;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = IfMaxStringSize + 1)]
            public string Description;
            public uint PhysicalAddressLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = IfMaxPhysAddressLength)]
            public byte[] PhysicalAddress;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = IfMaxPhysAddressLength)]
            public byte[] PermanentPhysicalAddress;
            public uint Mtu;
            public uint Type;
            public uint TunnelType;
            public uint MediaType;
            public uint PhysicalMediumType;
            public uint AccessType;
            public uint DirectionType;
            public byte InterfaceAndOperStatusFlags;
            public uint OperStatus;
            public uint AdminStatus;
            public uint MediaConnectState;
            public Guid NetworkGuid;
            public uint ConnectionType;
            public ulong TransmitLinkSpeed;
            public ulong ReceiveLinkSpeed;
            public ulong InOctets;
            public ulong InUcastPkts;
            public ulong InNUcastPkts;
            public ulong InDiscards;
            public ulong InErrors;
            public ulong InUnknownProtos;
            public ulong InUcastOctets;
            public ulong InMulticastOctets;
            public ulong InBroadcastOctets;
            public ulong OutOctets;
            public ulong OutUcastPkts;
            public ulong OutNUcastPkts;
            public ulong OutDiscards;
            public ulong OutErrors;
            public ulong OutUcastOctets;
            public ulong OutMulticastOctets;
            public ulong OutBroadcastOctets;
            public ulong OutQLen;
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetIfEntry2(ref MibIfRow2 row);

        public NetworkSnapshot(long bytesReceived, long bytesSent, int adapterCount, string displayName)
        {
            this.BytesReceived = bytesReceived;
            this.BytesSent = bytesSent;
            this.AdapterCount = adapterCount;
            this.DisplayName = displayName ?? string.Empty;
        }

        public long BytesReceived;
        public long BytesSent;
        public int AdapterCount;
        public string DisplayName;

        public bool HasAdapters
        {
            get { return this.AdapterCount > 0; }
        }

        public long TotalBytes
        {
            get { return this.BytesReceived + this.BytesSent; }
        }

        public static List<AdapterListItem> GetAdapterItems()
        {
            List<AdapterListItem> items = new List<AdapterListItem>();
            NetworkInterface[] interfaces;

            try
            {
                interfaces = NetworkInterface.GetAllNetworkInterfaces();
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce("network-getallnetworkinterfaces-adapterlist", "Failed to enumerate network interfaces for adapter list.", ex);
                return items;
            }

            foreach (NetworkInterface networkInterface in interfaces)
            {
                if (!NetworkAdapterClassifier.IsSelectableInSetup(networkInterface))
                {
                    continue;
                }

                bool isAvailable = NetworkAdapterClassifier.IsCapturable(networkInterface);
                OperationalStatus operationalStatus;
                string stateText = NetworkAdapterClassifier.TryGetOperationalStatus(networkInterface, out operationalStatus) &&
                    operationalStatus == OperationalStatus.Up
                    ? UiLanguage.Get("Calibration.AdapterStateActive", "aktiv")
                    : UiLanguage.Get("Calibration.AdapterStateInactive", "inaktiv");
                string displayText = string.Format("{0} ({1})", networkInterface.Name, stateText);
                items.Add(new AdapterListItem(
                    networkInterface.Id,
                    networkInterface.Name,
                    displayText,
                    isAvailable));
            }

            items.Sort(
                delegate(AdapterListItem left, AdapterListItem right)
                {
                    int availabilityComparison = right.IsAvailable.CompareTo(left.IsAvailable);
                    if (availabilityComparison != 0)
                    {
                        return availabilityComparison;
                    }

                    return string.Compare(left.DisplayText, right.DisplayText, StringComparison.CurrentCultureIgnoreCase);
                });

            return items;
        }

        public static NetworkSnapshot Capture(MonitorSettings settings)
        {
            if (settings == null || string.IsNullOrWhiteSpace(settings.AdapterId))
            {
                return new NetworkSnapshot(0L, 0L, 0, string.Empty);
            }

            NetworkInterface[] interfaces;

            try
            {
                interfaces = NetworkInterface.GetAllNetworkInterfaces();
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce("network-getallnetworkinterfaces-capture", "Failed to enumerate network interfaces for capture.", ex);
                return new NetworkSnapshot(0L, 0L, 0, string.Empty);
            }

            if (settings.UsesAutomaticAdapterSelection())
            {
                return CaptureAutomatic(interfaces);
            }

            return CaptureSelectedAdapter(interfaces, settings);
        }

        public static AdapterAvailabilityState GetAdapterAvailabilityState(MonitorSettings settings)
        {
            if (settings == null || string.IsNullOrEmpty(settings.AdapterId))
            {
                return AdapterAvailabilityState.Missing;
            }

            NetworkInterface[] interfaces;

            try
            {
                interfaces = NetworkInterface.GetAllNetworkInterfaces();
            }
            catch (Exception ex)
            {
                AppLog.WarnOnce("network-getallnetworkinterfaces-availability", "Failed to enumerate network interfaces for availability check.", ex);
                return AdapterAvailabilityState.Missing;
            }

            if (settings.UsesAutomaticAdapterSelection())
            {
                return SelectAutomaticAdapter(interfaces) != null
                    ? AdapterAvailabilityState.Available
                    : AdapterAvailabilityState.Missing;
            }

            foreach (NetworkInterface networkInterface in interfaces)
            {
                if (!NetworkAdapterClassifier.IsSelectableInSetup(networkInterface))
                {
                    continue;
                }

                if (!string.Equals(networkInterface.Id, settings.AdapterId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return NetworkAdapterClassifier.IsCapturable(networkInterface)
                    ? AdapterAvailabilityState.Available
                    : AdapterAvailabilityState.Inactive;
            }

            return AdapterAvailabilityState.Missing;
        }

        public static string ResolveAdapterKey(MonitorSettings settings)
        {
            if (settings != null && !string.IsNullOrWhiteSpace(settings.AdapterId))
            {
                if (!settings.UsesAutomaticAdapterSelection())
                {
                    return settings.AdapterId.Trim();
                }

                NetworkInterface[] interfaces;

                try
                {
                    interfaces = NetworkInterface.GetAllNetworkInterfaces();
                }
                catch (Exception ex)
                {
                    AppLog.WarnOnce("network-getallnetworkinterfaces-adapterkey", "Failed to enumerate network interfaces for adapter key resolution.", ex);
                    return MonitorSettings.AutomaticAdapterId;
                }

                NetworkInterface primaryAdapter = SelectAutomaticAdapter(interfaces);
                if (primaryAdapter == null || string.IsNullOrWhiteSpace(primaryAdapter.Id))
                {
                    return MonitorSettings.AutomaticAdapterId;
                }

                return primaryAdapter.Id.Trim();
            }

            return string.Empty;
        }

        private static NetworkSnapshot CaptureAutomatic(NetworkInterface[] interfaces)
        {
            NetworkInterface primaryAdapter = SelectAutomaticAdapter(interfaces);
            if (primaryAdapter == null)
            {
                return new NetworkSnapshot(0L, 0L, 0, string.Empty);
            }

            long bytesReceived;
            long bytesSent;
            if (!TryReadStatistics(primaryAdapter, out bytesReceived, out bytesSent))
            {
                return new NetworkSnapshot(0L, 0L, 0, primaryAdapter.Name);
            }

            return new NetworkSnapshot(bytesReceived, bytesSent, 1, primaryAdapter.Name);
        }

        private static NetworkSnapshot CaptureSelectedAdapter(NetworkInterface[] interfaces, MonitorSettings settings)
        {
            foreach (NetworkInterface networkInterface in interfaces)
            {
                if (!NetworkAdapterClassifier.IsSelectableInSetup(networkInterface))
                {
                    continue;
                }

                if (!string.Equals(networkInterface.Id, settings.AdapterId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!NetworkAdapterClassifier.IsCapturable(networkInterface))
                {
                    return new NetworkSnapshot(0L, 0L, 0, networkInterface.Name);
                }

                long bytesReceived;
                long bytesSent;
                if (!TryReadStatistics(networkInterface, out bytesReceived, out bytesSent))
                {
                    return new NetworkSnapshot(0L, 0L, 0, networkInterface.Name);
                }

                return new NetworkSnapshot(bytesReceived, bytesSent, 1, networkInterface.Name);
            }

            return new NetworkSnapshot(0L, 0L, 0, settings.AdapterName);
        }









        private static NetworkInterface SelectAutomaticAdapter(NetworkInterface[] interfaces)
        {
            if (interfaces == null || interfaces.Length == 0)
            {
                return null;
            }

            NetworkInterface bestAdapter = null;
            int bestTypePriority = int.MinValue;
            long bestSpeed = long.MinValue;

            foreach (NetworkInterface networkInterface in interfaces)
            {
                if (!NetworkAdapterClassifier.IsSelectableInSetup(networkInterface) || !NetworkAdapterClassifier.IsCapturable(networkInterface))
                {
                    continue;
                }

                int typePriority = NetworkAdapterClassifier.GetAutomaticAdapterTypePriority(networkInterface);
                long speed;
                if (!NetworkAdapterClassifier.TryGetInterfaceSpeed(networkInterface, out speed))
                {
                    speed = 0L;
                }

                bool isBetterCandidate = bestAdapter == null;
                if (!isBetterCandidate && typePriority != bestTypePriority)
                {
                    isBetterCandidate = typePriority > bestTypePriority;
                }

                if (!isBetterCandidate && speed != bestSpeed)
                {
                    isBetterCandidate = speed > bestSpeed;
                }

                if (isBetterCandidate)
                {
                    bestAdapter = networkInterface;
                    bestTypePriority = typePriority;
                    bestSpeed = speed;
                }
            }

            return bestAdapter;
        }



        private static bool TryReadStatistics(
            NetworkInterface networkInterface,
            out long bytesReceived,
            out long bytesSent)
        {
            bytesReceived = 0L;
            bytesSent = 0L;

            if (networkInterface == null)
            {
                return false;
            }

            if (TryReadInterfaceOctetStatistics(networkInterface, out bytesReceived, out bytesSent))
            {
                return true;
            }

            AppLog.WarnOnce(
                "network-stats-fallback-ipv4-" + (networkInterface.Id ?? string.Empty),
                string.Format(
                    "Falling back to IPv4 statistics for adapter '{0}' after interface-counter measurement was unavailable.",
                    networkInterface.Name ?? string.Empty));

            try
            {
                IPv4InterfaceStatistics statistics = networkInterface.GetIPv4Statistics();
                bytesReceived = statistics.BytesReceived;
                bytesSent = statistics.BytesSent;
                return true;
            }
            catch (NetworkInformationException)
            {
                AppLog.WarnOnce(
                    "network-ipv4stats-failed-" + (networkInterface.Id ?? string.Empty),
                    string.Format(
                        "IPv4 statistics are unavailable for adapter '{0}'.",
                        networkInterface.Name ?? string.Empty));
                return false;
            }
            catch (NotImplementedException)
            {
                AppLog.WarnOnce(
                    "network-ipv4stats-notimplemented-" + (networkInterface.Id ?? string.Empty),
                    string.Format(
                        "IPv4 statistics are not implemented for adapter '{0}'.",
                        networkInterface.Name ?? string.Empty));
                return false;
            }
        }

        private static bool TryReadInterfaceOctetStatistics(
            NetworkInterface networkInterface,
            out long bytesReceived,
            out long bytesSent)
        {
            bytesReceived = 0L;
            bytesSent = 0L;

            uint interfaceIndex;
            if (!TryGetInterfaceIndex(networkInterface, out interfaceIndex) || interfaceIndex == 0U)
            {
                return false;
            }

            try
            {
                MibIfRow2 row = CreateEmptyMibIfRow2();
                row.InterfaceIndex = interfaceIndex;
                uint result = GetIfEntry2(ref row);
                if (result != NoError)
                {
                    AppLog.WarnOnce(
                        "network-getifentry2-result-" + interfaceIndex.ToString(),
                        string.Format(
                            "GetIfEntry2 failed for interface index {0} with result {1} (Win32 error 0x{2:X8}); falling back if possible.",
                            interfaceIndex,
                            result,
                            System.Runtime.InteropServices.Marshal.GetLastWin32Error()));
                    return false;
                }

                bytesReceived = ConvertUnsignedCounter(row.InOctets);
                bytesSent = ConvertUnsignedCounter(row.OutOctets);
                return true;
            }
            catch (DllNotFoundException)
            {
                AppLog.WarnOnce(
                    "network-getifentry2-dllmissing",
                    "iphlpapi.dll is not available; falling back from interface octet counters.");
                return false;
            }
            catch (EntryPointNotFoundException)
            {
                AppLog.WarnOnce(
                    "network-getifentry2-entrypoint",
                    "GetIfEntry2 is not available; falling back from interface octet counters.");
                return false;
            }
            catch (TypeLoadException)
            {
                AppLog.WarnOnce(
                    "network-getifentry2-typeload",
                    "Native interface-counter structure could not be loaded; falling back from interface octet counters.");
                return false;
            }
        }

        private static bool TryGetInterfaceIndex(NetworkInterface networkInterface, out uint interfaceIndex)
        {
            interfaceIndex = 0U;

            IPInterfaceProperties properties;
            if (!NetworkAdapterClassifier.TryGetIpProperties(networkInterface, out properties) || properties == null)
            {
                return false;
            }

            try
            {
                IPv4InterfaceProperties ipv4Properties = properties.GetIPv4Properties();
                if (ipv4Properties != null && ipv4Properties.Index > 0)
                {
                    interfaceIndex = (uint)ipv4Properties.Index;
                    return true;
                }
            }
            catch (NetworkInformationException ex)
            {
                System.Diagnostics.Trace.WriteLine(string.Format("[TrafficView] IPv4 properties unavailable: {0}", ex.Message));
            }

            try
            {
                IPv6InterfaceProperties ipv6Properties = properties.GetIPv6Properties();
                if (ipv6Properties != null && ipv6Properties.Index > 0)
                {
                    interfaceIndex = (uint)ipv6Properties.Index;
                    return true;
                }
            }
            catch (NetworkInformationException ex2)
            {
                System.Diagnostics.Trace.WriteLine(string.Format("[TrafficView] IPv6 properties unavailable: {0}", ex2.Message));
            }

            return false;
        }





        private static MibIfRow2 CreateEmptyMibIfRow2()
        {
            MibIfRow2 row = new MibIfRow2();
            row.Alias = string.Empty;
            row.Description = string.Empty;
            row.PhysicalAddress = new byte[IfMaxPhysAddressLength];
            row.PermanentPhysicalAddress = new byte[IfMaxPhysAddressLength];
            return row;
        }

        private static long ConvertUnsignedCounter(ulong value)
        {
            return value > long.MaxValue ? long.MaxValue : (long)value;
        }
    }
}
