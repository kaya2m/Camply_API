using Camply.Application.Common.Interfaces;
using Camply.Infrastructure.Options;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Infrastructure.Services
{
    public class CodeBuilderService : ICodeBuilderService
    {
        private readonly CodeSettings _codeSettings;
        private readonly Dictionary<string, CodeData> _activeCodes;

        public CodeBuilderService(IOptions<CodeSettings> codeSettings)
        {
            _codeSettings = codeSettings.Value;
            _activeCodes = new Dictionary<string, CodeData>();
        }

        public string GenerateSixDigitCode()
        {
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] data = new byte[4];
                rng.GetBytes(data);
                int value = Math.Abs(BitConverter.ToInt32(data, 0));

                string code = (value % 900000 + 100000).ToString();

                _activeCodes[code] = new CodeData
                {
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(_codeSettings.CodeExpirationMinutes)
                };

                return code;
            }
        }

        public string VerifyCode(string code)
        {
            if (IsCodeValid(code))
            {
                _activeCodes.Remove(code); 
                return "Code verified successfully";
            }

            return "Invalid or expired code";
        }

        public bool IsCodeValid(string code)
        {
            if (_activeCodes.TryGetValue(code, out CodeData codeData))
            {
                return DateTime.UtcNow < codeData.ExpiresAt;
            }

            return false;
        }

        public DateTime GetCodeExpirationTime(string code)
        {
            if (_activeCodes.TryGetValue(code, out CodeData codeData))
            {
                return codeData.ExpiresAt;
            }

            return DateTime.MinValue;
        }

        public string GetCodeForUser(string userId)
        {
            string code = GenerateSixDigitCode();
            _activeCodes[code].UserId = userId;
            return code;
        }

        private class CodeData
        {
            public DateTime CreatedAt { get; set; }
            public DateTime ExpiresAt { get; set; }
            public string UserId { get; set; }
            public CodeType Type { get; set; }
        }
    }
}

