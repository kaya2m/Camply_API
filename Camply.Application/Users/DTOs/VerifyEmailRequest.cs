﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Application.Users.DTOs
{
    public class VerifyEmailRequest
    {
        public string Email { get; set; }
        public string Code { get; set; }
    }
    public class ResendVerificationEmailRequest
    {
        public string Email { get; set; }
    }
}
