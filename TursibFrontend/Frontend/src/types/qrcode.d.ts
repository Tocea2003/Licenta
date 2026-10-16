// Tipuri minimale pentru pachetul `qrcode` (doar ce folosim).
// @types/qrcode trage după el @types/node, care schimbă tipul lui setTimeout în toată aplicația.
declare module 'qrcode' {
  interface QRCodeToDataURLOptions {
    margin?: number
    width?: number
    errorCorrectionLevel?: 'L' | 'M' | 'Q' | 'H'
    color?: { dark?: string; light?: string }
  }
  const QRCode: {
    toDataURL(text: string, options?: QRCodeToDataURLOptions): Promise<string>
  }
  export default QRCode
}
