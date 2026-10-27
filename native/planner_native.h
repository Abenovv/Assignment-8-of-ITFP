#ifndef PLANNER_NATIVE_H
#define PLANNER_NATIVE_H

#include <stdint.h>

#if defined(_WIN32)
#define PLANNER_API __declspec(dllexport)
#define PLANNER_CALL __cdecl
#else
#define PLANNER_API __attribute__((visibility("default")))
#define PLANNER_CALL
#endif

/* Ceiling percentage of available time used. No allocation or retained state.
   total_minutes: 0..1000000; available_minutes: 1..480.
   Invalid arguments return -1; valid results may exceed 100 percent. */
PLANNER_API int32_t PLANNER_CALL planner_load_percent(
    int32_t total_minutes, int32_t available_minutes);

#endif
