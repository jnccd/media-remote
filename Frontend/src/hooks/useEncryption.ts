function xorCipher(text: string, key: string): string {
  let output = "";
  for (let i = 0; i < text.length; i++) {
    const encryptedChar = text.charCodeAt(i) ^ key.charCodeAt(i % key.length);
    output += String.fromCharCode(encryptedChar);
  }
  return output;
}

export function getAuthHeaderForMediaControlAPI(password: string) {
  const isoNow = new Date().toISOString();

  const plaintext = `abc,${isoNow}`;

  return btoa(xorCipher(plaintext, password));
}
