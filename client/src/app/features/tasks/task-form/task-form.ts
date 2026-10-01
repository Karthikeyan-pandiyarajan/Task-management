import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { toErrorMessage } from '../../../core/http/problem-details';
import {
  DESCRIPTION_MAX_LENGTH,
  SaveTaskRequest,
  TASK_STATUSES,
  TASK_STATUS_LABELS,
  TITLE_MAX_LENGTH,
  TaskStatus,
} from '../../../core/models/task.models';
import { TaskApiService } from '../task-api.service';
import { toDateOnly, todayDateOnly } from '../task-dates';

/** Mirrors the API's create rule. Not applied on edit, so overdue tasks can still be updated or completed. */
function notInPast(control: AbstractControl<string>): ValidationErrors | null {
  return control.value && control.value < todayDateOnly() ? { pastDate: true } : null;
}

function notBlank(control: AbstractControl<string>): ValidationErrors | null {
  return control.value.trim().length === 0 ? { required: true } : null;
}

@Component({
  selector: 'app-task-form',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './task-form.html',
  styleUrl: './task-form.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TaskForm implements OnInit {
  private readonly api = inject(TaskApiService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  /** Route param ":id" (withComponentInputBinding); undefined on /tasks/new. */
  readonly id = input<string>();

  protected readonly isEdit = computed(() => !!this.id());
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly statuses = TASK_STATUSES;
  protected readonly statusLabels = TASK_STATUS_LABELS;
  protected readonly titleMax = TITLE_MAX_LENGTH;
  protected readonly descriptionMax = DESCRIPTION_MAX_LENGTH;
  protected readonly today = todayDateOnly();

  protected readonly form = inject(NonNullableFormBuilder).group({
    title: ['', [Validators.required, notBlank, Validators.maxLength(TITLE_MAX_LENGTH)]],
    description: ['', [Validators.maxLength(DESCRIPTION_MAX_LENGTH)]],
    status: ['Todo' as TaskStatus, [Validators.required]],
    dueDate: [todayDateOnly(), [Validators.required]],
  });

  ngOnInit(): void {
    const id = this.id();
    if (!id) {
      this.form.controls.dueDate.addValidators(notInPast);
      return;
    }

    this.loading.set(true);
    this.api
      .get(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (task) => {
          this.form.setValue({
            title: task.title,
            description: task.description ?? '',
            status: task.status,
            dueDate: toDateOnly(task.dueDate),
          });
          this.loading.set(false);
        },
        error: (err: unknown) => {
          this.error.set(toErrorMessage(err, 'Could not load the task.'));
          this.loading.set(false);
        },
      });
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.error.set(null);

    const value = this.form.getRawValue();
    const request: SaveTaskRequest = {
      title: value.title.trim(),
      description: value.description.trim() || null,
      status: value.status,
      dueDate: value.dueDate,
    };
    const id = this.id();
    const save$ = id ? this.api.update(id, request) : this.api.create(request);

    save$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => void this.router.navigateByUrl('/tasks'),
      error: (err: unknown) => {
        this.error.set(toErrorMessage(err, 'Could not save the task.'));
        this.saving.set(false);
      },
    });
  }
}
