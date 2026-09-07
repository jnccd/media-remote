// Shared authenticated token scheme with the .NET server. Uses PBKDF2-HMAC-SHA256 to derive an
// AES-256 key from the password, then AES-GCM (12-byte IV, 128-bit tag). Wire format is
// base64( iv(12) || ciphertext || tag(16) ), where the plaintext is an ISO-8601 UTC timestamp.
//
// Parameters are identical to the server (Server/Src/Helpers/TokenCrypto.cs). We prefer the
// browser's Web Crypto (crypto.subtle) but fall back to a pure-JS implementation (@noble) for
// non-secure contexts such as a phone reaching the server over plain http://<lan-ip>, where
// crypto.subtle is unavailable.

import { gcm } from "@noble/ciphers/aes";
import { pbkdf2 } from "@noble/hashes/pbkdf2";
import { sha256 } from "@noble/hashes/sha256";

const SALT_B64 = "TWVkaWFDb250cm9sU2FsdA==";
const ITERATIONS = 100_000;

const SALT = Uint8Array.from(atob(SALT_B64), (c) => c.charCodeAt(0));
const enc = new TextEncoder();

function toB64(bytes: Uint8Array): string {
  let bin = "";
  for (let i = 0; i < bytes.length; i++) bin += String.fromCharCode(bytes[i]);
  return btoa(bin);
}

export async function getAuthHeaderForMediaControlAPI(password: string): Promise<string> {
  const isoNow = new Date().toISOString();
  const iv = crypto.getRandomValues(new Uint8Array(12));

  let ct: Uint8Array;

  if (typeof crypto.subtle !== "undefined") {
    // Preferred path (secure context): native Web Crypto.
    const material = await crypto.subtle.importKey(
      "raw",
      enc.encode(password),
      "PBKDF2",
      false,
      ["deriveKey"],
    );
    const key = await crypto.subtle.deriveKey(
      { name: "PBKDF2", salt: SALT, iterations: ITERATIONS, hash: "SHA-256" },
      material,
      { name: "AES-GCM", length: 256 },
      false,
      ["encrypt"],
    );
    const result = await crypto.subtle.encrypt(
      { name: "AES-GCM", iv, tagLength: 128 },
      key,
      enc.encode(isoNow),
    );
    ct = new Uint8Array(result);
  } else {
    // Fallback (e.g. phone over plain http): pure-JS implementation.
    const key = pbkdf2(sha256, enc.encode(password), SALT, {
      c: ITERATIONS,
      dkLen: 32,
    });
    // @noble's gcm is a factory: gcm(key, nonce) returns a Cipher whose .encrypt() yields ct||tag.
    const cipher = gcm(key, iv);
    ct = cipher.encrypt(enc.encode(isoNow));
  }

  const bytes = new Uint8Array(12 + ct.length);
  bytes.set(iv, 0);
  bytes.set(ct, 12);
  return toB64(bytes);
}
