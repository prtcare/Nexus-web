import { useQuery } from '@tanstack/react-query'

import { subprojectsApi } from './subprojectsApi'

export function useSubproject(
    subprojectId: string | null | undefined,
) {
    return useQuery({
        queryKey: ['subproject', subprojectId],
        queryFn: () => subprojectsApi.get(subprojectId!),
        enabled: Boolean(subprojectId),
    })
}
