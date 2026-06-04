// Google OAuth 2.0 Service pentru autentificare

// Configurare Google OAuth
const GOOGLE_CLIENT_ID = import.meta.env.VITE_GOOGLE_CLIENT_ID || '173077967825-prrjoav0t8di4gle24iav1334ir1kj40.apps.googleusercontent.com'
const GOOGLE_REDIRECT_URI = import.meta.env.VITE_GOOGLE_REDIRECT_URI || 'http://localhost:5173'

export interface GoogleUser {
  id: string
  email: string
  name: string
  picture: string
  givenName: string
  familyName: string
}

export interface GoogleAuthResponse {
  credential: string
  select_by: string
}

class GoogleAuthService {
  private scriptLoaded = false
  private google: any = null
  private lastError: string | null = null

  /**
   * Încarcă Google Identity Services script
   */
  async loadGoogleScript(): Promise<void> {
    if (this.scriptLoaded) {
      return
    }

    return new Promise((resolve, reject) => {
      const script = document.createElement('script')
      script.src = 'https://accounts.google.com/gsi/client'
      script.async = true
      script.defer = true
      
      script.onload = () => {
        this.scriptLoaded = true
        this.google = (window as any).google
        console.log('✅ Google Identity Services script loaded')
        resolve()
      }
      
      script.onerror = () => {
        console.error('❌ Failed to load Google Identity Services script')
        reject(new Error('Failed to load Google script'))
      }
      
      document.head.appendChild(script)
    })
  }

  /**
   * Inițializează Google Sign-In
   */
  async initialize(): Promise<void> {
    if (!GOOGLE_CLIENT_ID) {
      throw new Error('GOOGLE_NOT_CONFIGURED')
    }

    await this.loadGoogleScript()

    if (!this.google) {
      throw new Error('Google Identity Services not loaded')
    }

    this.lastError = null
  }

  getLastError(): string | null {
    return this.lastError
  }

  /**
   * Render buton de Sign In cu Google
   */
  renderButton(element: HTMLElement, callback: (response: GoogleAuthResponse) => void): void {
    if (!this.google) {
      console.error('❌ Google not initialized')
      return
    }

    // Re-initialize with the actual callback for this button
    this.google.accounts.id.initialize({
      client_id: GOOGLE_CLIENT_ID,
      callback: callback,
      auto_select: false,
      cancel_on_tap_outside: true,
      error_callback: (error: any) => {
        this.lastError = error?.type || error?.message || 'unknown_error'
      },
    })

    this.google.accounts.id.renderButton(element, {
      type: 'standard',
      theme: 'outline',
      size: 'large',
      text: 'signin_with',
      shape: 'rectangular',
      logo_alignment: 'left',
      width: 300,
    })

    console.log('✅ Google Sign-In button rendered')
  }

  /**
   * Decriptează JWT token de la Google
   */
  parseJwt(token: string): GoogleUser | null {
    try {
      const base64Url = token.split('.')[1]
      if (!base64Url) {
        throw new Error('Invalid token format')
      }
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
      console.error('❌ Error parsing JWT:', error)
      return null
    }
  }

  /**
   * Validează token-ul Google pe backend și obține token local
   */
  async validateToken(googleToken: string): Promise<{ token: string; user: any } | null> {
    try {
      const apiUrl = import.meta.env.VITE_API_URL || 'http://localhost:5022/api'
      
      const response = await fetch(`${apiUrl}/auth/google`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          credential: googleToken,
        }),
      })

      if (!response.ok) {
        const error = await response.json()
        console.error('❌ Backend validation failed:', error)
        return null
      }

      const data = await response.json()
      console.log('✅ Google token validated by backend')
      
      return data
    } catch (error) {
      console.error('❌ Error validating Google token:', error)
      return null
    }
  }

  /**
   * Sign out din Google
   */
  signOut(): void {
    if (this.google?.accounts?.id) {
      this.google.accounts.id.disableAutoSelect()
      console.log('✅ Google Sign-Out complete')
    }
  }

  /**
   * Verifică dacă Google Services este disponibil
   */
  isAvailable(): boolean {
    return this.scriptLoaded && this.google !== null
  }
}

// Export singleton
export const googleAuthService = new GoogleAuthService()
