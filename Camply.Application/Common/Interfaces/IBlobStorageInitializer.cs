using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Application.Common.Interfaces
{
    public interface IBlobStorageInitializer
    {
        Task InitializeAsync();
    }
}
