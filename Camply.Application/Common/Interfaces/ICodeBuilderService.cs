using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Application.Common.Interfaces
{
    public interface ICodeBuilderService
    {
        string GenerateSixDigitCode();
        string VerifyCode(string code);
        bool IsCodeValid(string code);
        DateTime GetCodeExpirationTime(string code);
        string GetCodeForUser(string userId);
    }

    public enum CodeType
    {
        PasswordReset,
        EmailVerification,
        TwoFactorAuth
    }
}
