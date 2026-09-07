import { Component, HostListener, NgZone, OnDestroy, OnInit } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { filter, take } from 'rxjs/operators';
import { GuideService } from '../shared/services/guide.service';

interface GuideStep {
    target: string;
    route: string | null;
    queryParams?: Record<string, string>;
    icon: string;
    titleKey: string;
    textKey: string;
    scroll?: boolean;
    mobileScroll?: boolean;
    action?: 'expand' | 'click';
}

@Component({
    selector: 'app-guide',
    templateUrl: './guide.component.html',
    styleUrls: ['./guide.component.css'],
    standalone: false
})
export class GuideComponent implements OnInit, OnDestroy {
    public showHint = false;
    public showGuide = false;
    public activeGuide: string | null = null;
    public step = 0;
    public targetRect: { top: number; left: number; width: number; height: number } | null = null;
    private shouldScrollToTarget = false;
    private targetObserver?: MutationObserver;
    private targetResizeObserver?: ResizeObserver;
    private readonly actedTargets = new Set<string>();
    public guidePlacement: 'top' | 'bottom' = 'bottom';
    private readonly hintDismissedKey = 'recipehub-guide-hint-dismissed';
    private readonly completedGuidesKey = 'recipehub-completed-guides';
    public completedGuides = new Set<string>();
    public readonly guideIds = ['recipes', 'recipeDetail', 'recipeCreate', 'pantry', 'foodPlan', 'grocery'];
    public steps: GuideStep[] = [];
    private guideStartSubscription?: Subscription;
    private readonly guideSteps: Record<string, GuideStep[]> = {
        recipes: [
            { target: '[data-guide-target="recipes-browse"]', route: '/recipes', queryParams: { guide: 'recipes' }, icon: 'fa-book-open', titleKey: 'guide.recipesBrowseTitle', textKey: 'guide.recipesBrowseText', scroll: false },
            { target: '[data-guide-target="recipes-selection"]', route: null, icon: 'fa-shopping-cart', titleKey: 'guide.recipesSelectionTitle', textKey: 'guide.recipesSelectionText' },
            { target: '[data-guide-target="recipes-create"]', route: null, icon: 'fa-magic', titleKey: 'guide.recipesCreateTitle', textKey: 'guide.recipesCreateText' },
            { target: '[data-guide-target="recipes-filters"]', route: null, icon: 'fa-filter', titleKey: 'guide.recipesFiltersTitle', textKey: 'guide.recipesFiltersText' },
            { target: '[data-guide-target="recipes-detailed-filters"]', route: '/recipes', icon: 'fa-sliders-h', titleKey: 'guide.recipesDetailedFiltersTitle', textKey: 'guide.recipesDetailedFiltersText', scroll: false },
            { target: '[data-guide-target="recipes-browse"]', route: null, icon: 'fa-tag', titleKey: 'guide.recipesPricesTitle', textKey: 'guide.recipesPricesText', scroll: false }
        ],
        recipeDetail: [
            { target: '[data-guide-target="recipe-detail-actions"]', route: '/recipes', queryParams: { guide: 'recipeDetail' }, icon: 'fa-bolt', titleKey: 'guide.recipeDetailActionsTitle', textKey: 'guide.recipeDetailActionsText' },
            { target: '[data-guide-target="recipe-detail-facts"]', route: null, icon: 'fa-clock', titleKey: 'guide.recipeDetailFactsTitle', textKey: 'guide.recipeDetailFactsText' },
            { target: '[data-guide-target="recipe-detail-cost"]', route: null, icon: 'fa-tag', titleKey: 'guide.recipeDetailCostTitle', textKey: 'guide.recipeDetailCostText' },
            { target: '[data-guide-target="recipe-detail-content"]', route: null, icon: 'fa-utensils', titleKey: 'guide.recipeDetailContentTitle', textKey: 'guide.recipeDetailContentText' },
            { target: '[data-guide-target="recipe-detail-nutrition"]', route: null, icon: 'fa-chart-pie', titleKey: 'guide.recipeDetailNutritionTitle', textKey: 'guide.recipeDetailNutritionText' },
            { target: '[data-guide-target="recipe-detail-rating"]', route: null, icon: 'fa-star', titleKey: 'guide.recipeDetailRatingTitle', textKey: 'guide.recipeDetailRatingText' }
        ],
        recipeCreate: [
            { target: '[data-guide-target="recipe-create-header"]', route: '/recipes', queryParams: { guide: 'recipeCreate' }, icon: 'fa-pen', titleKey: 'guide.recipeCreateOverviewTitle', textKey: 'guide.recipeCreateOverviewText' },
            { target: '[data-guide-target="recipe-create-head"]', route: null, icon: 'fa-image', titleKey: 'guide.recipeCreateDetailsTitle', textKey: 'guide.recipeCreateDetailsText', action: 'expand' },
            { target: '[data-guide-target="recipe-create-body"]', route: null, icon: 'fa-align-left', titleKey: 'guide.recipeCreateInstructionsTitle', textKey: 'guide.recipeCreateInstructionsText', action: 'expand' },
            { target: '[data-guide-target="recipe-create-ingredients"]', route: null, icon: 'fa-list', titleKey: 'guide.recipeCreateIngredientsTitle', textKey: 'guide.recipeCreateIngredientsText', action: 'expand' },
            { target: '[data-guide-target="recipe-create-cosmetics"]', route: null, icon: 'fa-tags', titleKey: 'guide.recipeCreateMetadataTitle', textKey: 'guide.recipeCreateMetadataText', action: 'expand' },
            { target: '[data-guide-target="recipe-create-preview"]', route: null, icon: 'fa-eye', titleKey: 'guide.recipeCreatePreviewTitle', textKey: 'guide.recipeCreatePreviewText', action: 'click' },
            { target: '[data-guide-target="recipe-create-save"]', route: null, icon: 'fa-check', titleKey: 'guide.recipeCreateSaveTitle', textKey: 'guide.recipeCreateSaveText' }
        ],
        pantry: [
            { target: '[data-guide-target="pantry-overview"]', route: '/pantry', icon: 'fa-box', titleKey: 'guide.pantryOverviewTitle', textKey: 'guide.pantryOverviewText' },
            { target: '[data-guide-target="pantry-add"]', route: '/pantry', icon: 'fa-plus', titleKey: 'guide.pantryAddTitle', textKey: 'guide.pantryAddText' },
            { target: '[data-guide-target="pantry-photo"]', route: '/pantry', icon: 'fa-camera', titleKey: 'guide.pantryPhotoTitle', textKey: 'guide.pantryPhotoText' },
            { target: '[data-guide-target="pantry-current"]', route: '/pantry', icon: 'fa-list', titleKey: 'guide.pantryCurrentTitle', textKey: 'guide.pantryCurrentText', scroll: false, mobileScroll: true },
            { target: '[data-guide-target="pantry-generate"]', route: '/pantry', icon: 'fa-magic', titleKey: 'guide.pantryGenerateTitle', textKey: 'guide.pantryGenerateText' },
            { target: '[data-guide-target="pantry-match"]', route: '/pantry', icon: 'fa-search', titleKey: 'guide.pantryMatchTitle', textKey: 'guide.pantryMatchText' }
        ],
        foodPlan: [
            { target: '[data-guide-target="food-plan-week"]', route: '/food-plan', icon: 'fa-calendar-alt', titleKey: 'guide.foodPlanWeekTitle', textKey: 'guide.foodPlanWeekText' },
            { target: '[data-guide-target="food-plan-add"]', route: '/food-plan', icon: 'fa-plus', titleKey: 'guide.foodPlanAddTitle', textKey: 'guide.foodPlanAddText' },
            { target: '[data-guide-target="food-plan-days"]', route: '/food-plan', icon: 'fa-utensils', titleKey: 'guide.foodPlanDaysTitle', textKey: 'guide.foodPlanDaysText' },
            { target: '[data-guide-target="food-plan-groceries"]', route: '/food-plan', icon: 'fa-shopping-cart', titleKey: 'guide.foodPlanGroceriesTitle', textKey: 'guide.foodPlanGroceriesText' },
            { target: '[data-guide-target="food-plan-prices"]', route: '/food-plan', icon: 'fa-tag', titleKey: 'guide.foodPlanPricesTitle', textKey: 'guide.foodPlanPricesText' }
        ],
        grocery: [
            { target: '[data-guide-target="grocery-overview"]', route: '/grocery', icon: 'fa-shopping-basket', titleKey: 'guide.groceryOverviewTitle', textKey: 'guide.groceryOverviewText' },
            { target: '[data-guide-target="grocery-ingredients"]', route: '/grocery', icon: 'fa-list', titleKey: 'guide.groceryIngredientsTitle', textKey: 'guide.groceryIngredientsText' },
            { target: '[data-guide-target="grocery-offers"]', route: '/grocery', icon: 'fa-store', titleKey: 'guide.groceryOffersTitle', textKey: 'guide.groceryOffersText' }
        ]
    };

    constructor(private router: Router, private zone: NgZone, private guideService: GuideService) {
        this.router.events.pipe(filter(event => event instanceof NavigationEnd)).subscribe(() => {
            if (this.showGuide && this.activeGuide) {
                this.scheduleTargetMeasurement();
            }
        });
    }

    public ngOnInit(): void {
        this.guideStartSubscription = this.guideService.startRequest$.subscribe(() => this.start());
        this.showHint = typeof localStorage !== 'undefined' && localStorage.getItem(this.hintDismissedKey) !== 'true';
        if (typeof localStorage !== 'undefined') {
            try {
                const completed = JSON.parse(localStorage.getItem(this.completedGuidesKey) ?? '[]');
                if (Array.isArray(completed)) this.completedGuides = new Set(completed);
            } catch {
                this.completedGuides = new Set<string>();
            }
        }
    }

    public ngOnDestroy(): void {
        this.guideStartSubscription?.unsubscribe();
        this.targetObserver?.disconnect();
        this.targetResizeObserver?.disconnect();
    }

    public start(): void {
        this.showHint = false;
        this.showGuide = true;
        this.activeGuide = null;
        this.steps = [];
        this.targetRect = null;
        this.actedTargets.clear();
        this.dismissHint();
        this.activateStep();
    }

    public dismissHint(): void {
        this.showHint = false;
        if (typeof localStorage !== 'undefined') localStorage.setItem(this.hintDismissedKey, 'true');
    }

    public close(): void {
        this.showGuide = false;
        this.activeGuide = null;
        this.steps = [];
        this.targetRect = null;
        this.targetObserver?.disconnect();
        this.targetResizeObserver?.disconnect();
        this.actedTargets.clear();
    }

    public startGuide(guideId: string): void {
        this.activeGuide = guideId;
        const steps = this.isMobileViewport() && guideId === 'recipes'
            ? (this.guideSteps[guideId] ?? []).filter(step => ![
                '[data-guide-target="recipes-selection"]',
                '[data-guide-target="recipes-create"]',
                '[data-guide-target="recipes-detailed-filters"]'
            ].includes(step.target))
            : (this.guideSteps[guideId] ?? []);
        this.steps = steps.map((step, index) =>
            guideId === 'recipeDetail' && index === 0 && this.isRecipeDetailRoute()
                ? { ...step, route: null, queryParams: undefined }
                : guideId === 'recipes' && index === 0
                    ? { ...step, queryParams: { guide: 'recipes', guideRun: String(Date.now()) } }
                    : { ...step }
        );
        this.step = 0;
        this.actedTargets.clear();
        this.activateStep();
    }

    public backToGuides(): void {
        this.activeGuide = null;
        this.steps = [];
        this.targetRect = null;
        this.targetObserver?.disconnect();
        this.targetResizeObserver?.disconnect();
        this.actedTargets.clear();
    }

    public next(): void {
        if (this.step < this.steps.length - 1) {
            this.step++;
            this.actedTargets.clear();
            this.activateStep();
        }
        else {
            if (this.activeGuide) this.markGuideComplete(this.activeGuide);
            this.close();
        }
    }

    public isGuideCompleted(guideId: string): boolean {
        return this.completedGuides.has(guideId);
    }

    public markGuideComplete(guideId: string): void {
        this.completedGuides.add(guideId);
        if (typeof localStorage !== 'undefined') {
            localStorage.setItem(this.completedGuidesKey, JSON.stringify(Array.from(this.completedGuides)));
        }
    }

    public previous(): void {
        if (this.step > 0) {
            this.step--;
            this.activateStep();
        }
    }

    @HostListener('window:resize')
    @HostListener('window:scroll')
    public refreshTarget(): void {
        if (!this.showGuide) return;
        this.setTargetRect();
    }

    public get tooltipStyle(): Record<string, string> {
        if (!this.targetRect || typeof window === 'undefined') return {};
        if (this.isMobileViewport()) {
            return this.guidePlacement === 'top'
                ? { top: '3rem', left: '1rem', right: '1rem', bottom: 'auto', transform: 'none' }
                : { top: 'auto', left: '1rem', right: '1rem', bottom: '4.5rem', transform: 'none' };
        }

        const width = 340;
        const gap = 16;
        const left = this.targetRect.left + this.targetRect.width + gap + width <= window.innerWidth
            ? this.targetRect.left + this.targetRect.width + gap
            : Math.max(16, this.targetRect.left - width - gap);
        const estimatedHeight = 420;
        const top = Math.min(Math.max(16, this.targetRect.top), Math.max(16, window.innerHeight - estimatedHeight - 16));
        return { left: `${left}px`, top: `${top}px` };
    }

    private activateStep(): void {
        const currentStep = this.steps[this.step];
        const route = currentStep?.route;
        this.targetRect = null;
        this.guidePlacement = 'bottom';
        this.targetObserver?.disconnect();
        this.targetResizeObserver?.disconnect();
        this.targetObserver = undefined;
        this.shouldScrollToTarget = this.isMobileViewport()
            ? currentStep?.mobileScroll ?? currentStep?.scroll !== false
            : currentStep?.scroll !== false;
        if (!route) {
            this.scheduleTargetMeasurement();
            return;
        }
        if (this.isAlreadyOnRoute(route, currentStep?.queryParams)) {
            this.scheduleTargetMeasurement();
            return;
        }
        this.router.navigate([route], { queryParams: currentStep.queryParams }).then(() => this.scheduleTargetMeasurement());
    }

    private isAlreadyOnRoute(route: string, queryParams?: Record<string, string>): boolean {
        if (!this.router.url.startsWith(route)) return false;
        if (queryParams) {
            return Object.entries(queryParams).every(([key, value]) => this.router.parseUrl(this.router.url).queryParams[key] === value);
        }
        return this.router.url.startsWith(route);
    }

    private scheduleTargetMeasurement(): void {
        window.setTimeout(() => this.waitForTarget(), 0);
        this.zone.onStable.pipe(take(1)).subscribe(() => {
            this.waitForTarget();
        });
    }

    private isRecipeDetailRoute(): boolean {
        return this.router.url.includes('/recipe/') && !this.router.url.includes('/recipes');
    }

    private isMobileViewport(): boolean {
        return typeof window !== 'undefined' && window.matchMedia('(max-width: 991px)').matches;
    }

    private waitForTarget(attempt = 0): void {
        if (this.setTargetRect()) {
            this.observeTargetChanges();
            return;
        }
        if (attempt >= 50) return;
        this.observeTargetChanges();
        window.setTimeout(() => this.waitForTarget(attempt + 1), 200);
    }

    private observeTargetChanges(): void {
        if (!this.targetObserver && typeof MutationObserver !== 'undefined') {
            this.targetObserver = new MutationObserver(() => {
                this.setTargetRect();
            });
            this.targetObserver.observe(document.body, {
                childList: true,
                subtree: true
            });
        }
    }

    private setTargetRect(): boolean {
        if (!this.ensureRecipeCreateView()) {
            this.updateTargetRect(null);
            return false;
        }

        const targets = Array.from(document.querySelectorAll(this.steps[this.step]?.target ?? ''));
        const target = targets.find(candidate => {
            const rect = candidate.getBoundingClientRect();
            return rect.width > 0 && rect.height > 0;
        });
        if (!target) {
            this.updateTargetRect(null);
            return false;
        }

        this.observeTargetResize(target);
        this.applyStepAction(target);

        if (this.shouldScrollToTarget && !this.steps[this.step]?.action) {
            this.shouldScrollToTarget = false;
            target.scrollIntoView({ behavior: 'auto', block: 'center', inline: 'nearest' });
            this.updateTargetRect(null);
            window.requestAnimationFrame(() => {
                window.requestAnimationFrame(() => this.setTargetRect());
            });
            return false;
        }

        const rect = target.getBoundingClientRect();
        const padding = 8;
        this.updateTargetRect({
            top: Math.max(4, rect.top - padding),
            left: Math.max(4, rect.left - padding),
            width: Math.min(window.innerWidth - Math.max(4, rect.left - padding) - 4, rect.width + padding * 2),
            height: Math.min(window.innerHeight - Math.max(4, rect.top - padding) - 4, rect.height + padding * 2)
        });
        this.scheduleGuidePlacement();
        return true;
    }

    private scheduleGuidePlacement(): void {
        if (!this.targetRect || typeof window === 'undefined') return;

        window.requestAnimationFrame(() => {
            const dialog = document.querySelector<HTMLElement>('.guide-active-dialog');
            if (!dialog || !this.targetRect) return;

            const dialogHeight = dialog.getBoundingClientRect().height;
            if (!dialogHeight) return;

            const targetTop = this.targetRect.top;
            const targetBottom = targetTop + this.targetRect.height;
            const bottomPlacement = {
                top: Math.max(16, window.innerHeight - dialogHeight - 24),
                bottom: Math.max(16, window.innerHeight - 24)
            };
            const bottomOverlaps = bottomPlacement.bottom > targetTop && bottomPlacement.top < targetBottom;
            const targetCoversViewport = targetBottom - targetTop >= window.innerHeight * .75;
            const nextPlacement = bottomOverlaps && !targetCoversViewport ? 'top' : 'bottom';

            if (nextPlacement !== this.guidePlacement) {
                this.zone.run(() => this.guidePlacement = nextPlacement);
            }
        });
    }

    private updateTargetRect(rect: { top: number; left: number; width: number; height: number } | null): void {
        if (rect && this.targetRect &&
            rect.top === this.targetRect.top &&
            rect.left === this.targetRect.left &&
            rect.width === this.targetRect.width &&
            rect.height === this.targetRect.height) {
            return;
        }

        if (!rect && !this.targetRect) return;
        this.zone.run(() => this.targetRect = rect);
    }

    private observeTargetResize(target: Element): void {
        if (typeof ResizeObserver === 'undefined' || this.targetResizeObserver) return;

        this.targetResizeObserver = new ResizeObserver(() => this.setTargetRect());
        this.targetResizeObserver.observe(target);
    }

    private ensureRecipeCreateView(): boolean {
        if (this.activeGuide !== 'recipeCreate') return true;

        const isPreviewStep = this.steps[this.step]?.target.includes('recipe-create-preview');
        const selector = isPreviewStep
            ? 'button[aria-controls="recipe-create-preview-panel"]'
            : 'button[aria-controls="recipe-create-edit-panel"]';
        const tab = document.querySelector<HTMLButtonElement>(selector);
        if (!tab || tab.getAttribute('aria-selected') === 'true') return true;

        tab.click();
        return false;
    }

    private applyStepAction(target: Element): void {
        const step = this.steps[this.step];
        if (!step?.action || this.actedTargets.has(step.target)) return;

        const control = step.action === 'expand'
            ? target.querySelector<HTMLButtonElement>('.accordion-button')
            : target as HTMLButtonElement;
        if (!control) return;

        if (step.action === 'expand' && control.getAttribute('aria-expanded') === 'true') {
            this.actedTargets.add(step.target);
            return;
        }

        this.actedTargets.add(step.target);
        control.click();
        this.trackTargetTransition(target);
    }

    private trackTargetTransition(target: Element, frame = 0, stableFrames = 0, lastHeight = -1): void {
        this.setTargetRect();
        const height = target.getBoundingClientRect().height;
        const nextStableFrames = Math.abs(height - lastHeight) < 0.5 ? stableFrames + 1 : 0;
        if (nextStableFrames >= 3 || frame >= 90) {
            target.scrollIntoView({ behavior: 'auto', block: 'center', inline: 'nearest' });
            window.requestAnimationFrame(() => this.setTargetRect());
            return;
        }
        window.requestAnimationFrame(() => this.trackTargetTransition(target, frame + 1, nextStableFrames, height));
    }
}
