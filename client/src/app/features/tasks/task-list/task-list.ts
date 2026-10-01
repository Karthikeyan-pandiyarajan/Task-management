import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { rxResource, takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { toErrorMessage } from '../../../core/http/problem-details';
import { TASK_STATUSES, TASK_STATUS_LABELS, Task, TaskStatus } from '../../../core/models/task.models';
import { TaskApiService } from '../task-api.service';
import { toDateOnly, todayDateOnly } from '../task-dates';

@Component({
  selector: 'app-task-list',
  imports: [RouterLink, DatePipe],
  templateUrl: './task-list.html',
  styleUrl: './task-list.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TaskList {
  private readonly api = inject(TaskApiService);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly statuses = TASK_STATUSES;
  protected readonly statusLabels = TASK_STATUS_LABELS;

  protected readonly statusFilter = signal<TaskStatus | null>(null);
  /** Id of the task whose delete button was clicked once and now awaits confirmation. */
  protected readonly pendingDeleteId = signal<string | null>(null);
  protected readonly deletingId = signal<string | null>(null);
  protected readonly actionError = signal<string | null>(null);

  // Re-fetches whenever the filter changes; cancels in-flight requests and unsubscribes on destroy.
  protected readonly tasks = rxResource({
    params: () => ({ status: this.statusFilter() ?? undefined }),
    stream: ({ params }) => this.api.list(params.status),
  });

  protected readonly errorMessage = (error: unknown) => toErrorMessage(error, 'Could not load tasks.');

  protected onFilterChange(value: string): void {
    this.statusFilter.set(isTaskStatus(value) ? value : null);
  }

  protected isOverdue(task: Task): boolean {
    return task.status !== 'Done' && toDateOnly(task.dueDate) < todayDateOnly();
  }

  protected askDelete(id: string): void {
    this.actionError.set(null);
    this.pendingDeleteId.set(id);
  }

  protected cancelDelete(): void {
    this.pendingDeleteId.set(null);
  }

  protected confirmDelete(id: string): void {
    this.deletingId.set(id);
    this.api
      .delete(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.pendingDeleteId.set(null);
          this.deletingId.set(null);
          this.tasks.reload();
        },
        error: (err: unknown) => {
          this.deletingId.set(null);
          this.actionError.set(toErrorMessage(err, 'Could not delete the task.'));
        },
      });
  }
}

function isTaskStatus(value: string): value is TaskStatus {
  return (TASK_STATUSES as readonly string[]).includes(value);
}
