/**
 * @NApiVersion 2.x
 * @NScriptType ScheduledScript
 */
define(['N/format', 'N/log', 'N/record', 'N/search'], function (format, log, record, search) {
    function execute(context) {
        try {
            var currentDate = new Date();
            var currentMonth = currentDate.getMonth(); // June = 5, Dec = 11
            var currentYear = currentDate.getFullYear();
            if (currentMonth !== 4 && currentMonth !== 11) {
                log.audit("Script Skipped", "This script only runs June & Dec.");
                return;
            }

            var year = {
                "2020": 1, "2021": 2, "2022": 3, "2023": 4, "2024": 5, "2025": 6, "2026": 7, "2027": 8, "2028": 9, "2029": 10, "2030": 11, "2031": 12,
                "2032": 13, "2033": 14, "2034": 15, "2035": 16, "2036": 17, "2037": 18, "2038": 19, "2039": 20, "2040": 21
            };

            var customrecord_bonus_calculationSearchObj = search.create({
                type: "customrecord_bonus_calculation",
                filters:[
                    ["custrecord_employee_bonus.custentity_manager", "noneof", "@NONE@"],
                    "AND",
                    ["isinactive", "is", "F"]
                ],
                columns:[
                    search.createColumn({
                        name: "custentity_manager",
                        join: "CUSTRECORD_EMPLOYEE_BONUS",
                        summary: "GROUP",
                        label: "Manager"
                    }),
                    search.createColumn({
                        name: "custrecord_employee_bonus",
                        summary: "COUNT",
                        label: "Employee"
                    })
                ]
            });
            var bonusCalSearchCount = customrecord_bonus_calculationSearchObj.runPaged().count;
            log.debug("customrecord_bonus_calculationSearchObj result count", bonusCalSearchCount);
            var h1H2 = currentMonth <= 5 ? 1 : 2;

            customrecord_bonus_calculationSearchObj.run().each(function (empResults) {

                var csmEmployee = empResults.getValue({
                    name: "custentity_manager",
                    join: "CUSTRECORD_EMPLOYEE_BONUS",
                    summary: "GROUP"
                });
                var customrecord_manager_performance_evalSearchObj = search.create({
                    type: "customrecord_manager_performance_eval",
                    filters:[
                        ["custrecord_manager", "anyof", csmEmployee],
                        "AND",
                        ["custrecord_year_performance", "anyof", year[currentYear]],
                        "AND",
                        ["custrecord_h1_h2_performance", "anyof", h1H2]
                    ],
                    columns:[
                        search.createColumn({ name: "custrecord_manager", label: "Manager" }),
                        search.createColumn({ name: "custrecord_year_performance", label: "Year" }),
                        search.createColumn({ name: "custrecord_h1_h2_performance", label: "H1/ H2" })
                    ]
                });
                var managersRecCount = customrecord_manager_performance_evalSearchObj.runPaged().count;
                if (managersRecCount == 0) {
                    var managersRecord = record.create({
                        type: "customrecord_manager_performance_eval",
                        isDynamic: true
                    });
                    managersRecord.setValue({
                        fieldId: "custrecord_manager",
                        value: csmEmployee
                    });
                    managersRecord.setValue({
                        fieldId: "custrecord_year_performance",
                        value: year[currentYear]
                    });
                    managersRecord.setValue({
                        fieldId: 'custrecord_h1_h2_performance',
                        value: h1H2
                    });
                    managersRecord.setValue({
                        fieldId: 'custrecord_manager_weighted_average',
                        value: 0
                    });
                    var managersRecordId = managersRecord.save();
                    log.debug("Manager Performance Record Created", "Record ID: " + managersRecordId);
                }
                return true;
            });
        } catch (error) {
            log.error("Error in Creating Revenue Upsell Records", error);
        }
    }

    return {
        execute: execute
    };
});