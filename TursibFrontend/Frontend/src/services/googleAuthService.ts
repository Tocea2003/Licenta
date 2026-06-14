// Google OAuth 2.0 Service - authorization code flow (no origin checks)

const GOOGLE_CLIENT_ID = import.meta.env.VITE_GOOGLE_CLIENT_ID || '414414048044-0fq9i8hnoui70oc2j2tu2974ugmla9vl.apps.googleusercontent.com'

export interface GoogleUser {
  id: string
  email: string
  name: string
  picture: string
  givenName: string
  familyName: string
}

class GoogleAuthService {
  startLogin(): void {
    const redirectUri = window.location.origin + '/login'

    const params = new URLSearchParams({
      client_id: GOOGLE_CLIENT_ID,
      redirect_uri: redirectUri,
      response_type: 'code',
      scope: 'openid email profile',
      prompt: 'select_account',
    })

    window.location.href = `https://accounts.google.com/o/oauth2/v2/auth?${params}`
  }

  getCodeFromUrl(): string | null {
    const params = new URLSearchParams(window.location.search)
    return params.get('code')
  }

  clearUrlCode(): void {
    history.replaceState(null, '', window.location.pathname)
  }

  async exchangeCode(code: string): Promise<{ token: string; user: any } | null> {
    try {
      const apiUrl = import.meta.env.VITE_API_URL || 'http://localhost:5022/api'
      const redirectUri = window.location.origin + '/login'

      const response = await fetch(`${apiUrl}/auth/google`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ code, redirectUri }),
      })

      if (!response.ok) {
        const error = await response.json()
        console.error('Backend auth failed:', error)
        return null
      }

      return await response.json()
    } catch (error) {
      console.error('Error exchanging code:', error)
      return null
    }
  }
}

export const googleAuthService = new GoogleAuthService()
