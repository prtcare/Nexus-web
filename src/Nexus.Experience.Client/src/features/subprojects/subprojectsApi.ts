import { nexusApi } from '../../api/ApiClient'

import type {
    SubprojectDetails,
    SubprojectSummary,
} from './Subproject'

export const subprojectsApi = {
    list(
        projectId: string,
    ): Promise<SubprojectSummary[]> {
        return nexusApi.get<SubprojectSummary[]>(
            `/projects/${projectId}/subprojects`,
        )
    },

    get(
        subprojectId: string,
    ): Promise<SubprojectDetails> {
        return nexusApi.get<SubprojectDetails>(
            `/subprojects/${subprojectId}`,
        )
    },
}
