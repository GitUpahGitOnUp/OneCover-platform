export interface PlanRate {
  tier: string;
  monthlyPremium: number;
}

export interface Plan {
  id: number;
  name: string;
  type: string;
  description: string;
  benefitPercent: number;
  weeklyMaximum: number;
  eliminationDays: number;
  isActive: boolean;
  rates: PlanRate[];
}

export interface Enrollment {
  id: number;
  planId: number;
  planName: string;
  planType: string;
  tier: string;
  monthlyPremium: number;
  estimatedPerPaycheckDeduction: number;
  enrolledOn: string;
  status: string;
}
