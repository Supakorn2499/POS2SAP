using Microsoft.AspNetCore.Mvc;

namespace POS2SAP.API.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AuthorizeAttribute : Attribute
{
    public AuthorizeAttribute() { }
}
