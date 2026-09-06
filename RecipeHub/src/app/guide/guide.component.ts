import { Component, HostListener, NgZone, OnInit } from '@angular/core';
import { NavigationEnd, Router } from '@angular/router';
import { filter, take } from 'rxjs/operators';

interface GuideStep {
    target: string;
    route: string | null;
    queryParams?: Record<string, string>;
    icon: string;
    titleKey: string;
    textKey: string;
    scroll?: boolean;
}

@Component({
    selector: 'app-guide',
    templateUrl: './guide.component.html',
    styleUrls: ['./guide.component.css'],
    standalone: false
})
export class GuideComponent implements OnInit {
    public showHint = false;
    public showGuide = false;
    public activeGuide: string | null = null;
    public step = 0;
    public targetRect: { top: number; left: number; width: number; height: number } | null = null;
    private shouldScrollToTarget = false;
    private targetObserver?: MutationObserver;
    private readonly hintDismissedKey = 'recipehub-guide-hint-dismissed';
    public readonly guideIds = ['recipes', 'recipeDetail', 'pantry', 'foodPlan', 'grocery'];
    public steps: GuideStep[] = [];
    private readonly guideSteps: Record<string, GuideStep[]> = {
        recipes: [
            { target: '[data-guide-target="recipes-browse"]', route: '/recipes', icon: 'fa-book-open', titleKey: 'guide.recipesBrowseTitle', textKey: 'guide.recipesBrowseText', scroll: false },
            { target: '[data-guide-target="recipes-create"]', route: '/recipes', icon: 'fa-magic', titleKey: 'guide.recipesCreateTitle', textKey: 'guide.recipesCreateText' },
            { target: '[data-guide-target="recipes-filters"]', route: '/recipes', icon: 'fa-filter', titleKey: 'guide.recipesFiltersTitle', textKey: 'guide.recipesFiltersText' },
            { target: '[data-guide-target="recipes-browse"]', route: '/recipes', icon: 'fa-tag', titleKey: 'guide.recipesPricesTitle', textKey: 'guide.recipesPricesText', scroll: false }
        ],
        recipeDetail: [
            { target: '[data-guide-target="recipe-detail-actions"]', route: '/recipes', queryParams: { guide: 'recipeDetail' }, icon: 'fa-bolt', titleKey: 'guide.recipeDetailActionsTitle', textKey: 'guide.recipeDetailActionsText' },
            { target: '[data-guide-target="recipe-detail-facts"]', route: null, icon: 'fa-clock', titleKey: 'guide.recipeDetailFactsTitle', textKey: 'guide.recipeDetailFactsText' },
            { target: '[data-guide-target="recipe-detail-cost"]', route: null, icon: 'fa-tag', titleKey: 'guide.recipeDetailCostTitle', textKey: 'guide.recipeDetailCostText' },
            { target: '[data-guide-target="recipe-detail-content"]', route: null, icon: 'fa-utensils', titleKey: 'guide.recipeDetailContentTitle', textKey: 'guide.recipeDetailContentText' },
            { target: '[data-guide-target="recipe-detail-nutrition"]', route: null, icon: 'fa-chart-pie', titleKey: 'guide.recipeDetailNutritionTitle', textKey: 'guide.recipeDetailNutritionText' },
            { target: '[data-guide-target="recipe-detail-rating"]', route: null, icon: 'fa-star', titleKey: 'guide.recipeDetailRatingTitle', textKey: 'guide.recipeDetailRatingText' }
        ],
        pantry: [
            { target: '[data-guide-target="pantry-overview"]', route: '/pantry', icon: 'fa-box', titleKey: 'guide.pantryOverviewTitle', textKey: 'guide.pantryOverviewText' },
            { target: '[data-guide-target="pantry-add"]', route: '/pantry', icon: 'fa-plus', titleKey: 'guide.pantryAddTitle', textKey: 'guide.pantryAddText' },
            { target: '[data-guide-target="pantry-photo"]', route: '/pantry', icon: 'fa-camera', titleKey: 'guide.pantryPhotoTitle', textKey: 'guide.pantryPhotoText' },
            { target: '[data-guide-target="pantry-current"]', route: '/pantry', icon: 'fa-list', titleKey: 'guide.pantryCurrentTitle', textKey: 'guide.pantryCurrentText', scroll: false },
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

    constructor(private router: Router, private zone: NgZone) {
        this.router.events.pipe(filter(event => event instanceof NavigationEnd)).subscribe(() => {
            if (this.showGuide && this.activeGuide) {
                this.scheduleTargetMeasurement();
            }
        });
    }

    public ngOnInit(): void {
        this.showHint = typeof localStorage !== 'undefined' && localStorage.getItem(this.hintDismissedKey) !== 'true';
    }

    public start(): void {
        this.showHint = false;
        this.showGuide = true;
        this.activeGuide = null;
        this.steps = [];
        this.targetRect = null;
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
    }

    public startGuide(guideId: string): void {
        this.activeGuide = guideId;
        this.steps = (this.guideSteps[guideId] ?? []).map((step, index) =>
            guideId === 'recipeDetail' && index === 0 && this.isRecipeDetailRoute()
                ? { ...step, route: null, queryParams: undefined }
                : { ...step }
        );
        this.step = 0;
        this.activateStep();
    }

    public backToGuides(): void {
        this.activeGuide = null;
        this.steps = [];
        this.targetRect = null;
        this.targetObserver?.disconnect();
    }

    public next(): void {
        if (this.step < this.steps.length - 1) {
            this.step++;
            this.activateStep();
        }
        else this.close();
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
        this.targetObserver?.disconnect();
        this.targetObserver = undefined;
        this.shouldScrollToTarget = currentStep?.scroll !== false;
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
            this.targetObserver.observe(document.body, { childList: true, subtree: true });
        }
    }

    private setTargetRect(): boolean {
        const targets = Array.from(document.querySelectorAll(this.steps[this.step]?.target ?? ''));
        const target = targets.find(candidate => {
            const rect = candidate.getBoundingClientRect();
            return rect.width > 0 && rect.height > 0;
        });
        if (!target) {
            this.targetRect = null;
            return false;
        }

        if (this.shouldScrollToTarget) {
            this.shouldScrollToTarget = false;
            target.scrollIntoView({ behavior: 'auto', block: 'center', inline: 'nearest' });
        }

        const rect = target.getBoundingClientRect();
        const padding = 8;
        this.targetRect = {
            top: Math.max(4, rect.top - padding),
            left: Math.max(4, rect.left - padding),
            width: Math.min(window.innerWidth - Math.max(4, rect.left - padding) - 4, rect.width + padding * 2),
            height: Math.min(window.innerHeight - Math.max(4, rect.top - padding) - 4, rect.height + padding * 2)
        };
        return true;
    }
}
