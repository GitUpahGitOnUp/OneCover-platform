import { provideHttpClient } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { BehaviorSubject } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { API_BASE_URL } from '../../core/api/api-base-url.token';
import { AuthPage } from './auth-page';

describe('AuthPage', () => {
  let fixture: ComponentFixture<AuthPage>;
  let component: AuthPage;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AuthPage],
      providers: [
        provideHttpClient(),
        provideRouter([]),
        { provide: API_BASE_URL, useValue: 'http://localhost:5000' },
        {
          provide: ActivatedRoute,
          useValue: {
            data: new BehaviorSubject({ mode: 'register' }),
            snapshot: {
              queryParamMap: { get: () => null },
              queryParams: {},
            },
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(AuthPage);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => fixture.destroy());

  it('blocks hire dates later than today and exposes the calendar date limit', () => {
    const futureDate = new Date();
    futureDate.setDate(futureDate.getDate() + 1);
    const hireDate = component.form.controls.hireDate;
    hireDate.setValue(futureDate);
    hireDate.markAsTouched();
    fixture.detectChanges();

    expect(hireDate.hasError('futureDate')).toBe(true);
    expect(fixture.nativeElement.querySelector('mat-datepicker-toggle')).not.toBeNull();
    expect(component.latestHireDate.getTime()).toBeLessThan(futureDate.getTime());
    expect(fixture.nativeElement.querySelector('[role="alert"]')?.textContent).toContain(
      'Invalid hire date. The date cannot be later than today.',
    );
  });

  it('requires matching passwords during registration', () => {
    component.form.controls.password.setValue('correct-password');
    component.form.controls.passwordConfirmation.setValue('different-password');
    component.form.controls.passwordConfirmation.markAsTouched();
    fixture.detectChanges();

    expect(component.form.hasError('passwordMismatch')).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('Passwords do not match.');

    component.form.controls.passwordConfirmation.setValue('correct-password');

    expect(component.form.hasError('passwordMismatch')).toBe(false);
  });
});
