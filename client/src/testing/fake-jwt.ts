/** Builds an unsigned JWT-shaped string with the given expiry, for client-side tests only. */
export function fakeJwt(expiresInSeconds: number, email = 'demo@taskapp.com'): string {
  const encode = (value: object) =>
    btoa(JSON.stringify(value)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  const exp = Math.floor(Date.now() / 1000) + expiresInSeconds;
  return `${encode({ alg: 'HS256', typ: 'JWT' })}.${encode({ sub: 'user-1', email, exp })}.signature`;
}
