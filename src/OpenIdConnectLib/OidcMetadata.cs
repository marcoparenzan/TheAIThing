using System;
using System.Collections.Generic;
using System.Text;

namespace OpenIdConnectLib;

public record OidcMetadata(
    string Issuer,
    string AuthorizationEndpoint,
    string TokenEndpoint,
    string UserInfoEndpoint,
    string JwksUri);