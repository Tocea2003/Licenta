// Google OAuth 2.0 Service - redirect flow (no GIS library needed)

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
  /**
   * Redirect user to Google OAuth consent page
   */
  startLogin(): void {
    const redirectUri = window.location.origin + '/login'
    const nonce = crypto.randomUUID()
    sessionStorage.setItem('google_nonce', nonce)

    const params = new URLSearchParams({
      client_id: GOOGLE_CLIENT_ID,
      redirect_uri: redirectUri,
      response_type: 'id_token',
      scope: 'openid email profile',
      nonce: nonce,
      prompt: 'select_account',
    })

    window.location.href = `https://accounts.google.com/o/oauth2/v2/auth?${params}`
  }

  /**
   * Check if the current URL contains an id_token from Google redirect
   */
  getTokenFromUrl(): string | null {
    const hash = window.location.hash.substring(1)
    if (!hash) return null

    const params = new URLSearchParams(hash)
    return params.get('id_token')
  }

  /**
   * Clear the hash from the URL after extracting the token
   */
  clearUrlToken(): void {
    history.replaceState(null, '', window.location.pathname + window.location.search)
  }

  /**
   * Parse a Google ID token JWT
   */
  parseJwt(token: string): GoogleUser | null {
    try {
      const base64Url = token.split('.')[1]
      if (!base64Url) throw new Error('Invalid token format')

      const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/')
      const jsonPayload = decodeURIComponent(
        atob(base64)
          .split('')
          .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
          .join('')
      )
      const payload = JSON.parse(jsonPayload)

      return {
        id: payload.sub,
        email: payload.email,
        name: payload.name,
        picture: payload.picture,
        givenName: payload.given_name,
        familyName: payload.family_name,
      }
    } catch (error) {
      console.error('Error parsing JWT:', error)
      return null
    }
  }

  /**
   * Validate the Google token on the backend and get a local JWT
   */
  async validateToken(googleToken: string): Promise<{ token: string; user: any } | null> {
    try {
      const apiUrl = import.meta.env.VITE_API_URL || 'http://localhost:5022/api'

      const response = await fetch(`${apiUrl}/auth/google`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ credential: googleToken }),
      })

      if (!response.ok) {
        const error = await response.json()
        console.error('Backend validation failed:', error)
        return null
      }

      return await response.json()
    } catch (error) {
      console.error('Error validating Google token:', error)
      return null
    }
  }
}

export const googleAuthService = new GoogleAuthService()
