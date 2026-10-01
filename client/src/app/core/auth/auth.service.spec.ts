import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { fakeJwt } from '../../../testing/fake-jwt';
import { AuthService, SESSION_STORAGE_KEY, isExpired } from './auth.service';

describe('AuthService', () => {
  let http: HttpTestingController;

  function createService(): AuthService {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    http = TestBed.inject(HttpTestingController);
    return TestBed.inject(AuthService);
  }

  beforeEach(() => localStorage.clear());
  afterEach(() => {
    http?.verify();
    localStorage.clear();
  });

  it('starts logged out when nothing is stored', () => {
    const auth = createService();

    expect(auth.isLoggedIn()).toBeFalse();
    expect(auth.token()).toBeNull();
  });

  it('login posts credentials, then exposes and persists the session', () => {
    const auth = createService();
    const token = fakeJwt(3600);
    let completed = false;

    auth.login({ email: 'demo@taskapp.com', password: 'Demo@1234' }).subscribe(() => (completed = true));
    const req = http.expectOne('/api/auth/login');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ email: 'demo@taskapp.com', password: 'Demo@1234' });
    req.flush({ token, email: 'demo@taskapp.com' });

    expect(completed).toBeTrue();
    expect(auth.isLoggedIn()).toBeTrue();
    expect(auth.email()).toBe('demo@taskapp.com');
    expect(auth.token()).toBe(token);
    expect(JSON.parse(localStorage.getItem(SESSION_STORAGE_KEY)!).token).toBe(token);
  });

  it('failed login leaves the user logged out', () => {
    const auth = createService();

    auth.login({ email: 'demo@taskapp.com', password: 'wrong' }).subscribe({ error: () => undefined });
    http.expectOne('/api/auth/login').flush({ detail: 'Invalid credentials' }, { status: 401, statusText: 'Unauthorized' });

    expect(auth.isLoggedIn()).toBeFalse();
    expect(localStorage.getItem(SESSION_STORAGE_KEY)).toBeNull();
  });

  it('restores a valid session from storage after a page reload', () => {
    const token = fakeJwt(3600);
    localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify({ token, email: 'demo@taskapp.com' }));

    const auth = createService();

    expect(auth.isAuthenticated()).toBeTrue();
    expect(auth.email()).toBe('demo@taskapp.com');
  });

  it('ignores an expired stored token', () => {
    localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify({ token: fakeJwt(-60), email: 'demo@taskapp.com' }));

    const auth = createService();

    expect(auth.isAuthenticated()).toBeFalse();
    expect(localStorage.getItem(SESSION_STORAGE_KEY)).toBeNull();
  });

  it('logout clears the session and navigates to login with the return url', () => {
    localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify({ token: fakeJwt(3600), email: 'demo@taskapp.com' }));
    const auth = createService();
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

    auth.logout('/tasks/123/edit');

    expect(auth.isLoggedIn()).toBeFalse();
    expect(localStorage.getItem(SESSION_STORAGE_KEY)).toBeNull();
    expect(navigate).toHaveBeenCalledWith(['/login'], { queryParams: { returnUrl: '/tasks/123/edit' } });
  });

  it('isExpired treats malformed tokens as expired', () => {
    expect(isExpired('not-a-jwt')).toBeTrue();
    expect(isExpired(fakeJwt(3600))).toBeFalse();
  });
});
