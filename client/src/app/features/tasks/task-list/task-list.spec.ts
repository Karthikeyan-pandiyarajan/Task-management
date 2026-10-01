import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Task } from '../../../core/models/task.models';
import { TaskList } from './task-list';

const TASKS: Task[] = [
  { id: '1', title: 'Write report', description: 'Q3 numbers', status: 'Todo', dueDate: '2099-01-10T00:00:00' },
  { id: '2', title: 'Ship release', description: null, status: 'Done', dueDate: '2099-01-12T00:00:00' },
];

describe('TaskList', () => {
  let fixture: ComponentFixture<TaskList>;
  let http: HttpTestingController;
  let el: HTMLElement;

  // whenStable() would wait for the open HTTP request (a pending task) and never resolve, so instead run
  // effects (which trigger the resource request), let microtasks settle, then re-render.
  async function render(): Promise<void> {
    TestBed.tick();
    await new Promise((resolve) => setTimeout(resolve));
    TestBed.tick();
    fixture.detectChanges();
  }

  function query<T extends Element = HTMLElement>(selector: string): T | null {
    return el.querySelector<T>(selector);
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [TaskList],
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(TaskList);
    http = TestBed.inject(HttpTestingController);
    el = fixture.nativeElement as HTMLElement;
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('shows a loading state, then the tasks', async () => {
    await render();
    expect(query('[data-testid="loading"]')).not.toBeNull();

    http.expectOne('/api/tasks').flush(TASKS);
    await render();

    const items = el.querySelectorAll('[data-testid="task-item"]');
    expect(items.length).toBe(2);
    expect(items[0].textContent).toContain('Write report');
    expect(query('[data-testid="loading"]')).toBeNull();
  });

  it('shows an empty state when there are no tasks', async () => {
    await render();
    http.expectOne('/api/tasks').flush([]);
    await render();

    expect(query('[data-testid="empty"]')?.textContent).toContain('No tasks yet');
  });

  it('shows the API error message when loading fails', async () => {
    await render();
    http.expectOne('/api/tasks').flush({ title: 'Oops', detail: 'Database unavailable' }, { status: 500, statusText: 'Error' });
    await render();

    expect(query('[data-testid="load-error"]')?.textContent).toContain('Database unavailable');
  });

  it('re-queries the API with the selected status filter', async () => {
    await render();
    http.expectOne('/api/tasks').flush(TASKS);
    await render();

    const select = query<HTMLSelectElement>('[data-testid="status-filter"]')!;
    select.value = 'Done';
    select.dispatchEvent(new Event('change'));
    await render();

    const req = http.expectOne((r) => r.url === '/api/tasks' && r.params.get('status') === 'Done');
    req.flush([TASKS[1]]);
    await render();

    expect(el.querySelectorAll('[data-testid="task-item"]').length).toBe(1);
  });

  it('asks for confirmation before deleting, then reloads the list', async () => {
    await render();
    http.expectOne('/api/tasks').flush(TASKS);
    await render();

    query('[data-testid="delete"]')!.click();
    await render();
    http.expectNone({ method: 'DELETE' }); // first click only asks

    query('[data-testid="confirm-delete"]')!.click();
    await render();
    const del = http.expectOne({ method: 'DELETE', url: '/api/tasks/1' });
    del.flush(null, { status: 204, statusText: 'No Content' });
    await render();

    http.expectOne('/api/tasks').flush([TASKS[1]]);
    await render();
    expect(el.querySelectorAll('[data-testid="task-item"]').length).toBe(1);
  });
});
