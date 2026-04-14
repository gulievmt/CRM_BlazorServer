using CRMBlazorServerRBS.CustomCodes;
using Microsoft.AspNetCore.Components;

namespace CRMBlazorServerRBS
{
    public partial class App
    {
        [Inject]
        CRMBlazorServerRBS.CustomCodes.IRequestInfoProvider RequestInfoProvider123 { get; set; }
    }
}
