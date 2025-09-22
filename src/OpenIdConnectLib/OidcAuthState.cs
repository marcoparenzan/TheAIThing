using System;
using System.Collections.Generic;
using System.Text;

namespace OpenIdConnectLib;

public record OidcAuthState(
    string Realm,
    string CodeVerifier,
    string Nonce,
    string RedirectUri);