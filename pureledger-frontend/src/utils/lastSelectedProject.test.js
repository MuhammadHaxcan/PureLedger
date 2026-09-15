import { beforeEach, describe, expect, it } from 'vitest';
import {
  rememberLastSelectedProject,
  resolveLastSelectedProject,
} from './lastSelectedProject';

describe('last selected project', () => {
  beforeEach(() => localStorage.clear());

  it('uses a remembered authorized project and otherwise falls back to the first authorized project', () => {
    const projects = [{ id: 1 }, { id: 2 }];

    rememberLastSelectedProject(2);
    expect(resolveLastSelectedProject(projects)).toBe('2');

    rememberLastSelectedProject(99);
    expect(resolveLastSelectedProject(projects)).toBe('1');
  });
});
