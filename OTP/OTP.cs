using System;
using System.Linq;
using System.Security.Cryptography;

namespace proxy
{

    public static class OTP
    {

        public static string GetLastOTP(byte[] secret)
        {
            return GetTotp(secret, -30);
        }

        public static string GetNextOTP(byte[] secret)
        {
            return GetTotp(secret, 30);
        }

        public static string GetOTP(byte[] secret)
        {
            return GetTotp(secret, 0);
        }

        // all code below from https://github.com/zhiliangxu/TOTP
        private static string GetHotp(byte[] secret, long counter)
        {
            byte[] message = BitConverter.GetBytes(counter)
                .Reverse().ToArray(); // Assuming Intel machine (little endian)

            byte[] hash;
            using (HMACSHA1 hmac = new HMACSHA1(secret, true))
            {
                hash = hmac.ComputeHash(message);
            }
            int offset = hash[hash.Length - 1] & 0xf;
            int truncatedHash = ((hash[offset] & 0x7f) << 24) |
                ((hash[offset + 1] & 0xff) << 16) |
                ((hash[offset + 2] & 0xff) << 8) |
                (hash[offset + 3] & 0xff);
            int hotp = truncatedHash % 1000000; // 6-digit code and hence 10 power 6, that is a million
            return hotp.ToString("D6");
        }

        private static string GetTotp(byte[] secret, int drift)
        {
            DateTime epochStart = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            long counter = (long)Math.Floor(((DateTime.UtcNow - epochStart).TotalSeconds + drift) / 30);
            return GetHotp(secret, counter);
        }

    }

    static class EncodingExtensions
    {
        private static readonly byte[] mapping = { 26, 27, 28, 29, 30, 255, 255, 255, 255, 255,
                                        255, 255, 255, 255, 255, 0, 1, 2, 3, 4,
                                        5, 6, 7, 8, 9, 10, 11, 12, 13, 14,
                                        15, 16, 17, 18, 19, 20, 21, 22, 23, 24,
                                        25 };

        public static byte[] ToByteArray(this string secret)
        {
            secret = secret.ToUpperInvariant();
            byte[] byteArray = new byte[(secret.Length + 7) / 8 * 5];

            long shiftingNum = 0L;
            int srcCounter = 0;
            int destCounter = 0;
            for (int i = 0; i < secret.Length; i++)
            {
                long num = (long)mapping[secret[i] - 50];
                shiftingNum |= num << (35 - srcCounter * 5);

                if (srcCounter == 7 || i == secret.Length - 1)
                {
                    for (int j = 0; j < 5; j++)
                    {
                        byteArray[destCounter++] = (byte)((shiftingNum >> (32 - j * 8)) & 0xff);
                    }
                    shiftingNum = 0L;
                }
                srcCounter = (srcCounter + 1) % 8;
            }

            return byteArray;
        }
    }
}