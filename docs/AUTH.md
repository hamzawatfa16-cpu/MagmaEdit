# MagmaEdit Authentication

## User experience

MagmaEdit uses Google as the primary account sign-in method. The desktop application does not ask the user to type a Gmail address or password. The **Continue with Google** button opens the system browser, where Google handles account selection and authentication.

After authentication, Supabase returns an authorization code to a short-lived localhost callback owned by the running MagmaEdit process. The code is exchanged using the PKCE verifier, and the resulting Supabase session is stored locally using Windows DPAPI with `DataProtectionScope.CurrentUser`.

## Configuration

The application reads these non-secret configuration values from the process environment:

```text
MAGMAEDIT_SUPABASE_URL=https://<project-ref>.supabase.co
MAGMAEDIT_SUPABASE_PUBLISHABLE_KEY=<publishable-key>
```

Do not put a Supabase service-role key, Google client secret, refresh token, or access token in source control.

The Supabase client uses the public/publishable key from the application's configuration. The Google OAuth client secret stays inside the Supabase provider configuration and is never shipped in MagmaEdit.

## Supabase setup

1. Create or use a Supabase project.
2. In Authentication → Providers, enable Google.
3. Configure the Google OAuth client ID and client secret in the Supabase Google provider.
4. In Authentication → URL Configuration, allow the local MagmaEdit callback pattern used for development. For example, configure a loopback URL pattern that covers the dynamically selected port:

```text
http://127.0.0.1:*/oauth/callback
```

5. Configure the production redirect URL strategy before public release. The desktop application should use a registered application redirect/deep-link or a controlled loopback callback suitable for the shipped installer.

Supabase documents that OAuth redirect URLs must be allow-listed and recommends PKCE for authorization-code flows. citeturn659253search0turn659253search2

## Security model

The local session file is protected with Windows DPAPI and scoped to the current Windows user. A different Windows user on the same machine cannot decrypt the stored session through the normal DPAPI user scope.

The authorization code is short-lived and single-use, and MagmaEdit binds it to the PKCE verifier created for that sign-in attempt. citeturn659253search7

MagmaEdit stores the Supabase access and refresh tokens in the encrypted session store. It does not store the user's Google password.

## Current limitations

Authentication is currently account sign-in infrastructure, not a complete multi-user cloud project service. Project and media data remain local-first. MCP account-aware authorization is a later stage: the current local MCP identity and bearer-token transport must eventually be connected to the authenticated application identity before remote multi-user access is enabled.

## Future account features

The next authentication improvements are:

- account display and current-user menu
- explicit sign-out from the editor
- session-expiration/re-authentication UX
- account-aware MCP authorization
- production redirect/deep-link packaging
- optional additional OAuth providers after Google is stable
