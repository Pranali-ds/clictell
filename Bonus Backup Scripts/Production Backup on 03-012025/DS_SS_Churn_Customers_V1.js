/**
 * @NApiVersion 2.x
 * @NScriptType ScheduledScript
 */
define(['N/format', 'N/log', 'N/record', 'N/search', 'N/runtime'], function(format, log, record, search, runtime) {
    function execute(context) {
        try {
            var yearMap = {
                "2020": 1, "2021": 2, "2022": 3, "2023": 4, "2024": 5, "2025": 6, "2026": 7, "2027": 8, "2028": 9, "2029": 10, "2030": 11,
                "2031": 12, "2032": 13, "2033": 14, "2034": 15, "2035": 16, "2036": 17, "2037": 18, "2038": 19, "2039": 20, "2040": 21
            };

            var startDate = new Date(2024, 8, 1); // Start from Sep 2024
            var endDate = new Date(); // Current date

            // Fetch all CSM employees once
            var employeeSearchObj = search.create({
                type: "employee",
                filters: [
                    ["salesrole", "anyof", "1"],
                    "AND",
                    ["isinactive", "is", "F"]
                ],
                columns: [
                    search.createColumn({ name: "internalid", label: "Internal ID" })
                ]
            });

            var employees = [];
            employeeSearchObj.run().each(function(result) {
                employees.push(result.getValue("internalid"));
                return true;
            });

            log.debug("Total Employees Found", employees.length);

            while (startDate.getTime() <= endDate.getTime()) {
                var nextMonth = new Date(startDate.getFullYear(), startDate.getMonth() + 1, 1); // First day of next month
                var formattedStartDate = format.format({ value: startDate, type: format.Type.DATE });

                var splitMonth = startDate.getMonth() + 1; // 1-based month
                var splitYear = startDate.getFullYear();
                splitYear = yearMap[splitYear];

                log.debug("Processing", "CSMEmployee: " + employees + ", Month: " + splitMonth + ", Year: " + splitYear);

                employees.forEach(function(csmEmployee) {
                    log.debug("csmployee", csmEmployee);
                    var churnCustomerSearch = search.create({
                        type: "customrecord_churn_customer",
                        filters: [
                            ["custrecord_sales_rep", "anyof", csmEmployee],
                            "AND",
                            ["custrecord_month", "anyof", splitMonth],
                            "AND",
                            ["custrecord_year", "anyof", splitYear]
                        ],
                        columns: [
                            search.createColumn({
                                name: "internalid",
                                label: "Internal ID"
                            }),
                        ]
                    });

                    var searchResultCount = churnCustomerSearch.runPaged().count;

                    if (searchResultCount === 0) {
                        var churnCustomerRec = record.create({ type: 'customrecord_churn_customer' });
                        
                        churnCustomerRec.setValue({
                            fieldId: 'custrecord_sales_rep',
                            value: csmEmployee
                        });
                        churnCustomerRec.setValue({
                            fieldId: 'custrecord_year',
                            value: splitYear
                        });
                        churnCustomerRec.setValue({
                            fieldId: 'custrecord_month',
                            value: splitMonth
                        });
                        churnCustomerRec.setValue({
                            fieldId: 'custrecord_opening_balance',
                            value: 0
                        });
                        churnCustomerRec.setValue({
                            fieldId: 'custrecord_closing_balance',
                            value: 0
                        });
                        churnCustomerRec.setValue({
                            fieldId: 'custrecord_new_customer',
                            value: 0
                        });
                        churnCustomerRec.setValue({
                            fieldId: 'custrecord_churn_customer',
                            value: 0
                        });
                        churnCustomerRec.setValue({
                            fieldId: 'custrecord_monthly_average_churn',
                            value: 0
                        });
                        if (splitMonth <= 6) {
                            churnCustomerRec.setValue({
                                fieldId: 'custrecord_h1_h2',
                                value: 1
                            });
                        }
                        else{
                            churnCustomerRec.setValue({
                                fieldId: 'custrecord_h1_h2',
                                value: 2
                            });
                        }
                        var savedChurnCustomerId = churnCustomerRec.save();
                        log.debug("New Churn Record Created", savedChurnCustomerId);
                    }
                });

                startDate = nextMonth;
            }
        } catch (error) {
            log.error("Error in Scheduled Script", error);
        }
    }

    return { execute: execute };
});