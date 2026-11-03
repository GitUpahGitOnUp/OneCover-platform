import { DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { apiErrorMessage } from '../../core/api/api-error';
import { MemberProfile } from '../../core/auth/auth.models';
import { ProfileService } from './profile.service';

@Component({
  selector: 'app-profile-page',
  imports: [DatePipe, MatCardModule],
  templateUrl: './profile-page.html',
  styleUrl: './profile-page.scss',
})
export class ProfilePage implements OnInit {
  private readonly profileService = inject(ProfileService);

  readonly profile = signal<MemberProfile | null>(null);
  readonly errorMessage = signal('');
  readonly isLoading = signal(true);

  ngOnInit(): void {
    this.profileService.getMine().subscribe({
      next: (profile) => {
        this.profile.set(profile);
        this.isLoading.set(false);
      },
      error: (error: unknown) => {
        this.errorMessage.set(apiErrorMessage(error));
        this.isLoading.set(false);
      },
    });
  }
}
