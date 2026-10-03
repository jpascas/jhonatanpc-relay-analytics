import { Routes } from '@angular/router';
import { WeeklyStatusPage } from './weekly-status/weekly-status-page';

export const routes: Routes = [
  { path: '', component: WeeklyStatusPage, title: 'Weekly activity' },
  { path: '**', redirectTo: '' },
];
