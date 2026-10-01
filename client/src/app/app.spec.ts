import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { fakeJwt } from '../testing/fake-jwt';
import { App } from './app';
import { SESSION_STORAGE_KEY } from './core/auth/auth.service';

describe('App', () => {
  beforeEach(async () => {
    localStorage.clear();
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  afterEach(() => localStorage.clear());

  it('hides the session area when logged out', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="current-user"]')).toBeNull();
  });

  it('shows the logged-in email', async () => {
    localStorage.setItem(SESSION_STORAGE_KEY, JSON.stringify({ token: fakeJwt(3600), email: 'demo@taskapp.com' }));

    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();

    const user = (fixture.nativeElement as HTMLElement).querySelector('[data-testid="current-user"]');
    expect(user?.textContent).toContain('demo@taskapp.com');
  });
});
