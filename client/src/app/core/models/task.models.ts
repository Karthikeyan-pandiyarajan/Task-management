// Mirrors Application.Tasks DTOs. The API serializes the status enum as a string.

export const TASK_STATUSES = ['Todo', 'InProgress', 'Done'] as const;

export type TaskStatus = (typeof TASK_STATUSES)[number];

export const TASK_STATUS_LABELS: Record<TaskStatus, string> = {
  Todo: 'To do',
  InProgress: 'In progress',
  Done: 'Done',
};

export const TITLE_MAX_LENGTH = 100;
export const DESCRIPTION_MAX_LENGTH = 2000;

export interface Task {
  id: string;
  title: string;
  description: string | null;
  status: TaskStatus;
  /** ISO date-time without offset, e.g. "2026-10-02T00:00:00". */
  dueDate: string;
}

export interface SaveTaskRequest {
  title: string;
  description: string | null;
  status: TaskStatus;
  /** "yyyy-MM-dd" */
  dueDate: string;
}
