import { Routes } from '@angular/router';

import { Events } from './events/events';
import { PriorAuthHome } from './home/home';
import { HistoryList } from './history/history-list';
import { MemberSearch } from './member/member-search/member-search';
import { CaptureMember } from './member/capture-member/capture-member';
import { Registries } from './registries/registries';
import { ProcessRegistryViewer } from './registries/process-registry/process-registry';
import { QueryableRegistryViewer } from './registries/queryable-registry/queryable-registry';
import { ProcessShell } from './process/process-shell';
import { RequestedService } from './process/requested-service';
import { InformationStep, InformationStepRouteData } from './process/information-step';
import { RequestedServicesSummary } from './process/requested-services-summary';
import { ProviderSelection, ProviderSelectionRouteData } from './process/provider-selection';
import { ReviewPriorAuthorization } from './process/review-prior-authorization';

export const routes: Routes = [
  {
    path: '',
    component: PriorAuthHome
  },
  {
    path: 'history',
    component: HistoryList
  },
  {
    path: 'events',
    component: Events
  },
  {
    path: 'process',
    redirectTo: 'process/new/member-search',
    pathMatch: 'full'
  },
  {
    path: 'process/:processId',
    component: ProcessShell,
    children: [
      {
        path: '',
        redirectTo: 'member-search',
        pathMatch: 'full'
      },
      {
        path: 'member-search',
        component: MemberSearch
      },
      {
        path: 'capture-member',
        component: CaptureMember
      },
      {
        path: 'requested-service',
        component: RequestedService
      },
      {
        path: 'capture-mri-info',
        component: InformationStep,
        data: {
          stepName: 'CaptureMriInfoStep',
          eyebrow: 'MRI Information',
          title: 'Capture MRI details',
          submitLabel: 'Submit MRI Information',
          emptyMessage: 'There are no MRI questions pending for this process.',
          failureMessage: 'Unable to capture MRI information.',
          nextRoute: 'requested-services'
        } satisfies InformationStepRouteData
      },
      {
        path: 'confirm-ct-instead-of-mri',
        component: InformationStep,
        data: {
          stepName: 'ConfirmCtInsteadOfMriStep',
          eyebrow: 'CT Request',
          title: 'Confirm CT',
          submitLabel: 'Submit',
          emptyMessage: 'There is no CT confirmation pending for this process.',
          failureMessage: 'Unable to confirm the CT request.',
          nextRoute: 'requested-services'
        } satisfies InformationStepRouteData
      },
      {
        path: 'requested-services',
        component: RequestedServicesSummary
      },
      {
        path: 'requesting-provider',
        component: ProviderSelection,
        data: {
          searchSourceName: 'RequestingProviderSearchSource',
          stepName: 'CaptureRequestingProviderStep',
          role: 'Ordering provider',
          description: 'Choose the provider who ordered the service (the requesting provider).',
          title: 'Ordering Provider Search',
          confirmLabel: 'Use as Ordering Provider',
          savingLabel: 'Saving Ordering Provider...',
          failureMessage: 'Unable to capture the ordering provider.',
          nextRoute: 'servicing-provider'
        } satisfies ProviderSelectionRouteData
      },
      {
        path: 'servicing-provider',
        component: ProviderSelection,
        data: {
          searchSourceName: 'ServicingProviderSearchSource',
          stepName: 'CaptureServicingProviderStep',
          role: 'Servicing provider',
          description: 'Choose the facility where the service will be performed (the servicing provider).',
          title: 'Servicing Provider Search',
          confirmLabel: 'Use as Servicing Provider',
          savingLabel: 'Saving Servicing Provider...',
          failureMessage: 'Unable to capture the servicing provider.',
          nextRoute: 'review'
        } satisfies ProviderSelectionRouteData
      },
      {
        path: 'clinical-info',
        component: InformationStep,
        data: {
          stepName: 'CaptureClinicalInfoStep',
          eyebrow: 'Clinical Information',
          title: 'Clinical questions',
          submitLabel: 'Submit Answers',
          emptyMessage: 'There are no clinical questions pending for this process.',
          failureMessage: 'Unable to capture clinical information.',
          nextRoute: 'review'
        } satisfies InformationStepRouteData
      },
      {
        path: 'review',
        component: ReviewPriorAuthorization
      }
    ]
  },
  {
    path: 'registries',
    component: Registries,
    children: [
      {
        path: '',
        redirectTo: 'process',
        pathMatch: 'full'
      },
      {
        path: 'process',
        component: ProcessRegistryViewer
      },
      {
        path: 'queryable',
        component: QueryableRegistryViewer
      }
    ]
  }
];
