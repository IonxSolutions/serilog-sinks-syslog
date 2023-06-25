// Copyright 2018 Ionx Solutions (https://www.ionxsolutions.com)
// Ionx Solutions licenses this file to you under the Apache License,
// Version 2.0. You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Shouldly;
using static Serilog.Sinks.Syslog.Tests.Fixture;

namespace Serilog.Sinks.Syslog.Tests
{
    public class CertificateStoreProviderTests : IDisposable
    {
        // In Visual Studio, unit test can/do run in parallel, even for the different target frameworks. But these
        // tests are using a shared resource, namely, the Windows Certificate Store. So who is to say that if the
        // unit test for .NET 4.6.2 successfully adds the certificate to the store, but then the unit test for .NET
        // Core 3.1 checks the store for existence of the certificate, does that mean .NET Core 3.1 was successful
        // at adding the certificate to the store.
        //
        // Or what really ends up happening is that one test for a framework adds the certificate and is then also
        // able to complete the test and remove the certificate before the test for the other frameworks have been
        // able to complete. Therefore, when the other frameworks go to check for the existence of the certificate
        // in the store, it doesn't exist, so the test fails.
        //
        // What we really need is to be able to dynamically generate a certificate. But that functionality isn't
        // available in .NET 4.6.2 (it is available in 4.7.2).
        //
        // So another approach is to try and make these unit tests for the various frameworks execute serially. We
        // can't use the simple xUnit [Collection] attribute on the test class because that only effects the
        // individual tests of the class, not the class being run for multiple target frameworks, which also run
        // in multiple/separate 'testhost' processes too.
        //
        // In Windows, in order to serialize access to a resource across multiple processes, you use a named mutex.
        // That is what we will do here, such that the first process to acquire the mutex will run to completion
        // while any other processes will block and wait. Note, if an error occurs while possessing the mutex, the
        // next process to acquire the mutex will have to handle the AbandonedMutexException.
        private Mutex singleTestHostMutex = null;

        public CertificateStoreProviderTests()
        {
            singleTestHostMutex = new Mutex(false, "CertificateStoreProviderTests");

            Retry:

            try
            {
                singleTestHostMutex.WaitOne();

                using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);

                store.Open(OpenFlags.ReadWrite | OpenFlags.OpenExistingOnly);
                store.Add(ClientCert);
            }
            catch (AbandonedMutexException)
            {
                // Some other process must have crashed or didn't release the mutex properly. This is okay, we
                // can just try again.
                goto Retry;
            }
        }

        [WindowsOnlyFact]
        public void Can_open_certificate_from_store()
        {
            var storeProvider = new CertificateStoreProvider(StoreName.My, StoreLocation.CurrentUser, ClientCertThumbprint);
            storeProvider.Certificate.ShouldNotBeNull();
            storeProvider.Certificate.Thumbprint.ShouldBe(ClientCertThumbprint, StringCompareShould.IgnoreCase);
        }

        [WindowsOnlyFact]
        public void Should_throw_when_no_certificate_with_thumbprint()
        {
            Should.Throw<ArgumentException>(() =>
                new CertificateStoreProvider(StoreName.My, StoreLocation.CurrentUser, "myergen"));
        }

        public void Dispose()
        {
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            
            store.Open(OpenFlags.ReadWrite | OpenFlags.OpenExistingOnly);

            var cert = store.Certificates.Cast<X509Certificate2>().FirstOrDefault(c => c.Thumbprint.Equals(ClientCertThumbprint, StringComparison.OrdinalIgnoreCase));
            
            store.Remove(cert);

            // We must use the certificate object that we got out of the certificate store. Otherwise, the call to
            // delete the persisted private key file will fail because the information won't match.
            DeletePrivateKey(cert);

            singleTestHostMutex.ReleaseMutex();
        }

        /// <summary>Attempt to delete the private key file associated with the specified certificate.</summary>
        /// <param name="cert">The certificate that was loaded with a persisted key.</param>
        /// <remarks>Only attempts to delete from the UserKeyset, which on Windows will be under the following:
        /// C:\Users\[user]\AppData\Roaming\Microsoft\Crypto\RSA\S-1-5-21-....</remarks>
        private static void DeletePrivateKey(X509Certificate2 cert)
        {
            if (cert == null)
            {
                return;
            }

            try
            {
                var privateKey = cert.GetRSAPrivateKey();

                // The private key of our test certificate is of type RSACng. Unfortunately, that is not supported
                // on all .NET Framework versions that we target. Therefore, to get this to work, we are going to
                // use reflection to get the information out of the key that we need.
                var key = privateKey.GetType().GetProperty("Key").GetValue(privateKey);
                var keyName = key.GetType().GetProperty("KeyName").GetValue(key) as string;

                // The provider name is nested in a Provider object, under a property called Provider.
                var provider = key.GetType().GetProperty("Provider").GetValue(key);
                var providerName = provider.GetType().GetProperty("Provider").GetValue(provider) as string;

                var filename = key.GetType().GetProperty("UniqueName").GetValue(key) as string;

                // When the certificate is created/loaded, we didn't specify where to store the private key, so it
                // should default to the users key set. Therefore, we need to do the same here, which again means not
                // explicitly specifying the key storage flag, like the MachineKeySet.
                SafeCspHandle cspHandle = null;
                if (!CryptAcquireContext(out cspHandle, keyName, providerName, ProviderType.RsaFull, CryptAcquireContextFlags.DeleteKeyset))
                {
                    Console.WriteLine($"Unable to delete private key of demo certificate ({filename}). Error: {Marshal.GetLastWin32Error()}");
                }

                cspHandle.Dispose();
            }
            catch (CryptographicException ex)
            {
                if (ex.HResult != NTE_BAD_KEYSET)
                {
                    throw;
                }
            }
        }

        /// <summary>Should have error message "Keyset does not exist".</summary>
        private static readonly int NTE_BAD_KEYSET = unchecked((int)0x80090016);

        // Taken from reflecting on System.Security.Cryptography.CapiNative.UnsafeNativeMethods
        // or the open source .NET reference source code: https://referencesource.microsoft.com/#mscorlib/system/security/cryptography/capinative.cs,094f077f570a3cb0
        // How to call the method was taken from: https://www.sysadmins.lv/blog-en/how-to-properly-delete-certificate-with-private-key-in-powershell.aspx
        [DllImport("advapi32", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptAcquireContext(out SafeCspHandle phProv, string pszContainer, string pszProvider, ProviderType dwProvType, CryptAcquireContextFlags dwFlags);

        [Flags]
        private enum CryptAcquireContextFlags
        {
            None = 0x0,
            NewKeyset = 0x8,
            DeleteKeyset = 0x10,
            MachineKeyset = 0x20,
            Silent = 0x40,
            VerifyContext = unchecked((int)0xF0000000)
        }

        private enum ProviderType
        {
            RsaFull = 1
        }

        private sealed class SafeCspHandle : Microsoft.Win32.SafeHandles.SafeHandleZeroOrMinusOneIsInvalid
        {
            private SafeCspHandle() : base(true)
            {
            }

            [DllImport("advapi32")]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool CryptReleaseContext(IntPtr hProv, int dwFlags);

            protected override bool ReleaseHandle()
            {
                return CryptReleaseContext(handle, 0);
            }
        }
    }
}
