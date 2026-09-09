export interface SubprojectSummary {
    subprojectId: string
    name: string
    status: string
    reference: string
    createdAt: string
}

export interface SubprojectDetails {
    subprojectId: string
    projectId: string
    name: string
    description: string
    status: string
    reference: string
    createdAt: string
}
