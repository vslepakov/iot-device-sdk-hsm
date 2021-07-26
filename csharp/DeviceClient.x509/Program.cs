using Microsoft.Azure.Devices.Client;
using System;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Text;

namespace DeviceClient.x509
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var cts = new CancellationTokenSource();
            var cancellationToken = cts.Token;
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();

            var config = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("config.json", optional: false)
               .Build();

            var appConfig = config.Get<AppConfig>();

            var auth = GetIdCertWithPrivateKeyHandle(appConfig);
            await StartSendingDataAsync(appConfig, auth, cancellationToken);

            WhenCancelled(cancellationToken).Wait();
        }

        private static DeviceAuthenticationWithX509Certificate GetIdCertWithPrivateKeyHandle(AppConfig appConfig)
        {
            Console.WriteLine("Getting Identity certificate...");

            var certChain = new X509Certificate2Collection();

            foreach(var caCertPath in appConfig.CaCertPaths)
            {
                var caCert = new X509Certificate2(caCertPath);
                certChain.Add(caCert);
            }

            var deviceCert = new X509Certificate2(appConfig.DeviceCert);
            var pkeyHandle = GetPrivateKeyHandle(appConfig);
            
#pragma warning disable CA1416 // Validate platform compatibility
            deviceCert = deviceCert.CopyWithPrivateKey(new RSAOpenSsl(pkeyHandle));
#pragma warning restore CA1416 // Validate platform compatibility

            return new DeviceAuthenticationWithX509Certificate(appConfig.DeviceId, deviceCert, certChain);
        }

        private static async Task StartSendingDataAsync(AppConfig appConfig, DeviceAuthenticationWithX509Certificate auth,
            CancellationToken cancellationToken)
        {
            Console.WriteLine($"Connecting to IoT Hub {appConfig.IotHubHostname} using x509 auth...");
            using var deviceClient = Microsoft.Azure.Devices.Client.DeviceClient.Create(appConfig.IotHubHostname, auth, TransportType.Mqtt_Tcp_Only);

            await deviceClient.OpenAsync(cancellationToken);
            Console.WriteLine("Device connection SUCCESS.");

            string telemetryPayload = "{{ \"temperature\": 10d }}";
            using var message = new Message(Encoding.UTF8.GetBytes(telemetryPayload))
            {
                ContentEncoding = "utf-8",
                ContentType = "application/json",
            };

            await deviceClient.SendEventAsync(message, cancellationToken);
            Console.WriteLine("Message sent!");
        }

        private static SafeEvpPKeyHandle GetPrivateKeyHandle(AppConfig appConfig)
        {
            Console.WriteLine($"Using OpenSSL Engine {appConfig.OpenSslEngine}");

            var engine = NativeMethods.ENGINE_by_id(appConfig.OpenSslEngine);
            _ = NativeMethods.ENGINE_init(engine);
            var pkey = NativeMethods.ENGINE_load_private_key(engine, appConfig.DeviceCertPrivateKeyHsmHandle, IntPtr.Zero, IntPtr.Zero);

#pragma warning disable CA1416 // Validate platform compatibility
            Console.WriteLine($"OpenSSL version: {SafeEvpPKeyHandle.OpenSslVersion}");

            var pkeyHandle = new SafeEvpPKeyHandle(pkey, true);
            if (pkeyHandle.IsInvalid)
#pragma warning restore CA1416 // Validate platform compatibility
            {
                throw new InvalidOperationException($"Engine: unable to find private key with handle: {appConfig.DeviceCertPrivateKeyHsmHandle}");
            }

            return pkeyHandle;
        }

        private static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }
    }
}
