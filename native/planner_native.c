#include "planner_native.h"

int32_t PLANNER_CALL planner_load_percent(
    int32_t total_minutes, int32_t available_minutes)
{
    if (total_minutes < 0 || total_minutes > 1000000 ||
        available_minutes < 1 || available_minutes > 480) {
        return -1;
    }

    const int64_t numerator = (int64_t)total_minutes * 100;
    return (int32_t)((numerator + available_minutes - 1) / available_minutes);
}
