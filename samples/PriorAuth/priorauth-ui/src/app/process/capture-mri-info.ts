import { computed, Component, effect, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';

import { ProcessService, ProcessErrorResponse } from '../kaleido/services/process-service';
import {
    QuestionnaireAnswerOption,
    QuestionnaireEnableWhen,
    QuestionnaireItem
} from '../kaleido/models/questionnaire';
import { buildProcessRoute } from './services/process-navigation';
import { ProcessStateService } from './services/process-state-service';

type MriBodyPart = 'Spine' | 'Knee';
type Laterality = 'None' | 'Left' | 'Right' | 'Bilateral';
type ContrastOption = 'WithoutContrast' | 'WithContrast' | 'WithAndWithoutContrast';

interface CaptureMriInfoStep {
    bodyPart: MriBodyPart;
    laterality: Laterality;
    contrast: ContrastOption;
}

@Component({
    selector: 'priorauth-capture-mri-info',
    standalone: true,
    imports: [FormsModule],
    templateUrl: './capture-mri-info.html',
    styleUrl: './capture-mri-info.scss'
})
export class CaptureMriInfo {
    constructor() {
        effect(() => {
            const bodyPartQuestion = this.bodyPartQuestion();
            const lateralityQuestion = this.lateralityQuestion();
            const contrastQuestion = this.contrastQuestion();
            const bodyPart = this.bodyPart;

            this.bodyPart = this.getResolvedValue(
                bodyPart,
                bodyPartQuestion,
                this.bodyPartOptions());

            this.laterality = this.getResolvedValue(
                this.laterality,
                lateralityQuestion,
                this.lateralityOptions());

            this.contrast = this.getResolvedValue(
                this.contrast,
                contrastQuestion,
                this.contrastOptions());

            if (!this.isQuestionEnabled(lateralityQuestion)) {
                this.laterality = this.getDefaultValue<Laterality>(lateralityQuestion, this.lateralityOptions())
                    ?? this.lateralityOptions()[0]
                    ?? this.laterality;
            }
        });
    }

    private readonly processService =
        inject(ProcessService);

    private readonly processState =
        inject(ProcessStateService);

    private readonly router =
        inject(Router);

    bodyPart: MriBodyPart = 'Spine';
    laterality: Laterality = 'None';
    contrast: ContrastOption = 'WithoutContrast';
    readonly isSubmitting =
        signal(false);
    readonly errorMessage =
        signal<string | undefined>(undefined);

    readonly questionnaire =
        computed(() =>
            this.processState.state().questionnaireStepName === 'CaptureMriInfo'
                ? this.processState.state().questionnaire
                : undefined);
    readonly title =
        computed(() => this.questionnaire()?.title ?? 'Capture MRI details');
    readonly description =
        computed(() => this.questionnaire()?.description);
    readonly bodyPartQuestion =
        computed(() => this.getQuestion('BodyPart'));
    readonly lateralityQuestion =
        computed(() => this.getQuestion('Laterality'));
    readonly contrastQuestion =
        computed(() => this.getQuestion('Contrast'));
    readonly bodyPartOptions =
        computed(() => this.getOptions<MriBodyPart>(this.bodyPartQuestion()));
    readonly lateralityOptions =
        computed(() => this.getOptions<Laterality>(this.lateralityQuestion()));
    readonly contrastOptions =
        computed(() => this.getOptions<ContrastOption>(this.contrastQuestion()));

    submit(): void {
        if (!this.processState.state().processId || this.isSubmitting()) {
            return;
        }

        this.isSubmitting.set(true);
        this.errorMessage.set(undefined);

        this.processService
            .executeStep<CaptureMriInfoStep, object>('CaptureMriInfo', {
                processId: this.processState.state().processId,
                processStep: {
                    bodyPart: this.bodyPart,
                    laterality: this.laterality,
                    contrast: this.contrast
                }
            })
            .subscribe({
                next: () => {
                    this.isSubmitting.set(false);
                    void this.router.navigate(
                        buildProcessRoute(
                            this.processState.state().processId,
                            'requested-services'));
                },
                error: error => {
                    this.isSubmitting.set(false);
                    this.errorMessage.set(this.getErrorMessage(error));
                }
            });
    }

    private getQuestion(
        bindingKey: string
    ): QuestionnaireItem | undefined {
        return this.questionnaire()?.items.find(
            item => item.bindingKey === bindingKey);
    }

    private getOptions<TOption extends string>(
        question: QuestionnaireItem | undefined
    ): TOption[] {
        return question?.answerOptions.map(
            option => option.value as TOption) ?? [];
    }

    getOptionLabel(
        question: QuestionnaireItem | undefined,
        value: string
    ): string {
        return question?.answerOptions.find(
            option => option.value === value)?.displayText ?? value;
    }

    isQuestionEnabled(
        question: QuestionnaireItem | undefined
    ): boolean {
        if (!question || question.enableWhen.length === 0) {
            return true;
        }

        return question.enableWhen.every(condition =>
            this.evaluateEnableWhen(condition));
    }

    trackOption(
        index: number,
        option: QuestionnaireAnswerOption
    ): string {
        return `${index}:${option.value}`;
    }

    private evaluateEnableWhen(
        condition: QuestionnaireEnableWhen
    ): boolean {
        const answer = this.getAnswerValue(condition.questionBindingKey);

        switch (condition.operator) {
            case 'Equals':
                return answer === condition.answerValue;
            case 'NotEquals':
                return answer !== condition.answerValue;
            default:
                return true;
        }
    }

    private getAnswerValue(
        bindingKey: string
    ): string | undefined {
        switch (bindingKey) {
            case 'BodyPart':
                return this.bodyPart;
            case 'Laterality':
                return this.laterality;
            case 'Contrast':
                return this.contrast;
            default:
                return undefined;
        }
    }

    private getResolvedValue<TOption extends string>(
        currentValue: TOption,
        question: QuestionnaireItem | undefined,
        options: TOption[]
    ): TOption {
        if (options.includes(currentValue)) {
            return currentValue;
        }

        return this.getDefaultValue(question, options)
            ?? options[0]
            ?? currentValue;
    }

    private getDefaultValue<TOption extends string>(
        question: QuestionnaireItem | undefined,
        options: TOption[]
    ): TOption | undefined {
        const defaultValue = question?.defaultValue as TOption | undefined;

        return defaultValue && options.includes(defaultValue)
            ? defaultValue
            : undefined;
    }

    private getErrorMessage(
        error: unknown
    ): string {
        if (ProcessErrorResponse.is(error) && error.messages.length > 0) {
            return error.messages
                .map(message => message.message)
                .join(' ');
        }

        return 'Unable to capture MRI information.';
    }
}
