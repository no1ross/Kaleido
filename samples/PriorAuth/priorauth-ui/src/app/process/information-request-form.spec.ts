import { TestBed } from '@angular/core/testing';

import { InformationRequest, InformationResponseItem } from '../kaleido/models/information-request';
import { InformationRequestForm } from './information-request-form';

// The MRI questions: laterality applies only when the body part is not the spine.
const mri: InformationRequest = {
    informationRequestId: 'mri|1',
    items: [
        {
            id: 'BodyPart', text: 'Which body part?', type: 'choice', repeats: false, items: [],
            options: [{ value: 'Spine' }, { value: 'Knee' }]
        },
        {
            id: 'Laterality', text: 'Which side?', type: 'choice', repeats: false, items: [],
            options: [{ value: 'Left' }, { value: 'Right' }],
            enableWhen: [{ question: 'BodyPart', operator: 'notEquals', answer: 'Spine' }]
        },
        {
            id: 'Contrast', text: 'Contrast?', type: 'choice', repeats: false, items: [],
            options: [{ value: 'WithContrast' }, { value: 'WithoutContrast' }],
            proposedAnswers: [{ value: 'WithoutContrast' }]
        }
    ]
};

describe('InformationRequestForm', () => {
    async function render() {
        await TestBed.configureTestingModule({ imports: [InformationRequestForm] }).compileComponents();

        const fixture = TestBed.createComponent(InformationRequestForm);
        fixture.componentRef.setInput('request', mri);
        fixture.detectChanges();
        await fixture.whenStable();

        const submitted: InformationResponseItem[][] = [];
        fixture.componentInstance.submitted.subscribe(items => submitted.push(items));

        return { fixture, form: fixture.componentInstance, submitted };
    }

    const questionIds = (form: InformationRequestForm) => form.questions().map(question => question.id);

    it('asks a conditional question only once the answers make it apply', async () => {
        const { fixture, form } = await render();

        expect(questionIds(form)).toEqual(['BodyPart', 'Contrast']);
        expect((fixture.nativeElement as HTMLElement).textContent).not.toContain('Which side?');

        form.setValue(mri.items[0], 'Knee');
        fixture.detectChanges();

        expect(questionIds(form)).toEqual(['BodyPart', 'Laterality', 'Contrast']);
        expect((fixture.nativeElement as HTMLElement).textContent).toContain('Which side?');
        expect(form.isComplete()).toBe(false);
    });

    it('pre-fills proposed answers for the user to confirm', async () => {
        const { form } = await render();

        expect(form.valueOf(mri.items[2])).toBe('WithoutContrast');
    });

    it('never sends the answer of a question that no longer applies', async () => {
        const { form, submitted } = await render();

        form.setValue(mri.items[0], 'Knee');
        form.setValue(mri.items[1], 'Left');
        form.setValue(mri.items[0], 'Spine');

        expect(form.isComplete()).toBe(true);
        form.submit();

        expect(submitted).toEqual([[
            { itemId: 'BodyPart', answers: [{ value: 'Spine' }] },
            { itemId: 'Contrast', answers: [{ value: 'WithoutContrast' }] }
        ]]);
    });
});
