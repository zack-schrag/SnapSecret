using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnapSecret.Application.Abstractions
{
    public interface ISecretsInfrastructureProvider
    {
        Task BuildAsync();
    }
}
