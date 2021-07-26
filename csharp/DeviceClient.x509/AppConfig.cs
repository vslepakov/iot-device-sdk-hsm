namespace DeviceClient.x509
{
    internal class AppConfig
    {
        public string IotHubHostname { get; set; }

        public string DeviceId { get; set; }

        public string OpenSslEngine { get; set; }

        public string[] CaCertPaths { get; set; }

        public string DeviceCert { get; set; }

        public string DeviceCertPrivateKeyHsmHandle { get; set; }
    }
}
