import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let token: string | null;
  let logout: jasmine.Spy<(returnUrl?: string) => void>;

  beforeEach(() => {
    token = 'abc.def.ghi';
    logout = jasmine.createSpy('logout');

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: AuthService, useValue: { token: () => token, logout } satisfies Partial<AuthService> },
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => backend.verify());

  it('adds the bearer token to API calls', () => {
    http.get('/api/tasks').subscribe();

    const req = backend.expectOne('/api/tasks');
    expect(req.request.headers.get('Authorization')).toBe('Bearer abc.def.ghi');
    req.flush([]);
  });

  it('does not add a header when the user is logged out', () => {
    token = null;

    http.get('/api/tasks').subscribe();

    const req = backend.expectOne('/api/tasks');
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush([]);
  });

  it('never sends the token to other hosts', () => {
    http.get('https://example.com/data').subscribe();

    const req = backend.expectOne('https://example.com/data');
    expect(req.request.headers.has('Authorization')).toBeFalse();
    req.flush({});
  });

  it('logs out and redirects on 401 from a protected endpoint', () => {
    let status = 0;
    http.get('/api/tasks').subscribe({ error: (e: { status: number }) => (status = e.status) });

    backend.expectOne('/api/tasks').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(logout).toHaveBeenCalledTimes(1);
    expect(status).toBe(401); // the error still reaches the caller
  });

  it('does not log out on 401 from the login endpoint (wrong password)', () => {
    http.post('/api/auth/login', {}).subscribe({ error: () => undefined });

    backend.expectOne('/api/auth/login').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(logout).not.toHaveBeenCalled();
  });

  it('does not log out on other errors', () => {
    http.get('/api/tasks/1').subscribe({ error: () => undefined });

    backend.expectOne('/api/tasks/1').flush(null, { status: 404, statusText: 'Not Found' });

    expect(logout).not.toHaveBeenCalled();
  });
});
