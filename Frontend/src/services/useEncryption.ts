function xorCipher(text: string, key: string): string {
  let output = "";
  for (let i = 0; i < text.length; i++) {
    const encryptedChar = text.charCodeAt(i) ^ key.charCodeAt(i % key.length);
    output += String.fromCharCode(encryptedChar);
  }
  return output;
}

function randomAsciiString(length: number): string {
  const chars =
    "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
  let result = "";
  for (let i = 0; i < length; i++) {
    const idx = Math.floor(Math.random() * chars.length);
    result += chars[idx];
  }
  return result;
}

export function getAuthHeaderForMediaControlAPI(password: string) {
  const isoNow = new Date().toISOString();

  const plaintext = `${randomAsciiString(
    Math.floor(Math.random() * 7) + 2
  )},${isoNow}`;

  return btoa(xorCipher(plaintext, password));
}
