//Bonus Calculation script 
/**
 * @NApiVersion 2.1
 * @NScriptType MapReduceScript
 */
define(['N/search', 'N/log', 'N/record'], function (search, log, record) {
    function getInputData() {
        var bonusCalculationData = {};
        var year = {
            1: "2020", 2: "2021", 3: "2022", 4: "2023", 5: "2024", 6: "2025", 7: "2026", 8: "2027", 9: "2028", 10: "2029", 11: "2030",
            12: "2031", 13: "2032", 14: "2033", 15: "2034", 16: "2035", 17: "2036", 18: "2037", 19: "2038", 20: "2039", 21: "2040"
        };

        var csatResultsCount = 0;

        // Churn Customers
        var customrecord_churn_customerSearchObj = search.create({
            type: "customrecord_churn_customer",
            filters:[
                ["custrecord_kpi_achievement", "isnotempty", ""],
                "AND",
                ["isinactive", "is", "F"]
            ],
            columns:[
                search.createColumn({ name: "custrecord_sales_rep", label: "Employee" }),
                search.createColumn({ name: "custrecord_h1_h2", label: "H1/ H2" }),
                search.createColumn({ name: "custrecord_year", label: "Year" }),
                search.createColumn({ name: "custrecord_kpi_achievement", label: "KPI Achievement %" })
            ]
        });
        var searchResultCount = customrecord_churn_customerSearchObj.runPaged().count;
        log.debug("customrecord_churn_customerSearchObj result count", searchResultCount);
        customrecord_churn_customerSearchObj.run().each(function (churnResults) {
            var churnEmployee = churnResults.getValue('custrecord_sales_rep');
            var churnH1H2 = churnResults.getValue('custrecord_h1_h2');
            var churnYear = churnResults.getValue('custrecord_year');
            var kpiAchievement = parseFloat(churnResults.getValue('custrecord_kpi_achievement'));
            var churnWeightage = parseFloat(25 / 100);
            var churnKPIWeightage = parseFloat(kpiAchievement / 100);
            var finalChurnWeightage = parseFloat(churnWeightage * churnKPIWeightage);
            var yearKey = year[churnYear];
            var key = `${churnEmployee}-${churnH1H2}-${yearKey}`;
            if (!bonusCalculationData[key]) {
                bonusCalculationData[key] = {
                    employee: churnEmployee,
                    h1h2: churnH1H2,
                    year: yearKey,
                    churn: finalChurnWeightage,
                    churnKpi: kpiAchievement,
                    csat: 0,
                    upsell: 0,
                    total: 0
                }
            } else {
                bonusCalculationData[key].churn = finalChurnWeightage;
                bonusCalculationData[key].churnKpi = kpiAchievement;
            }
            return true;
        });

        //CSAT Score
        var customrecord_csat_scoreSearchObj = search.create({
            type: "customrecord_csat_score",
            filters:[
                ["custrecord_kpi_achievement_csat", "isnotempty", ""],
                "AND",
                ["isinactive", "is", "F"],
                "AND",
                ["custrecord_static_weighted_avg", "isnotempty", ""]
            ],
            columns:[
                search.createColumn({name: "custrecord_employee_csat", summary: "GROUP",label: "Employee"}),
                search.createColumn({name: "custrecord_year_csat", summary: "GROUP",label: "Yaer"}),
                search.createColumn({name: "custrecord_kpi_achievement_csat", summary: "GROUP",label: "KPI Achievement %"}),
                search.createColumn({name: "custrecord_h1_h2_csat", summary: "GROUP",label: "H1/ H2"}),
                search.createColumn({name: "custrecord_static_weighted_avg", summary: "GROUP",label: "Static Weighted Avg"})
            ]
        });
        var csatsearchResultCount = customrecord_csat_scoreSearchObj.runPaged().count;
        log.debug("customrecord_csat_scoreSearchObj result count", csatsearchResultCount);
        customrecord_csat_scoreSearchObj.run().each(function (csatResults) {

            var csatEmployee = csatResults.getValue({
                name: "custrecord_employee_csat",
                summary: "GROUP"
            });
            var csatYear = csatResults.getValue({
                name: "custrecord_year_csat",
                summary: "GROUP"
            });
            var csatKPIAchievement = parseFloat(csatResults.getValue({
                name: "custrecord_kpi_achievement_csat",
                summary: "GROUP"
            }));
            var csatH1H2 = csatResults.getValue({
                name: "custrecord_h1_h2_csat",
                summary: "GROUP"
            });
            var staticWeightedAverage = parseFloat(csatResults.getValue({
                name: "custrecord_static_weighted_avg",
                summary: "GROUP"
            }));
            csatResultsCount = staticWeightedAverage;

            var csatWeightage = 0;
            if (csatResultsCount > 2) {
                csatWeightage = parseFloat(25 / 100);
            }
            var csatKPIWeightage = parseFloat(csatKPIAchievement / 100);
            var finalCSATWeightage = parseFloat(csatWeightage * csatKPIWeightage);
            var csatYearKey = year[csatYear];
            var csatKey = `${csatEmployee}-${csatH1H2}-${csatYearKey}`;
            if (!bonusCalculationData[csatKey]) {
                bonusCalculationData[csatKey] = {
                    employee: csatEmployee,
                    h1h2: csatH1H2,
                    year: csatYearKey,
                    churn: 0,
                    csat: finalCSATWeightage,
                    csatKpi: csatKPIAchievement,
                    upsell: 0,
                    total: 0
                }
            } else {
                bonusCalculationData[csatKey].csat = finalCSATWeightage;
                bonusCalculationData[csatKey].csatKpi = csatKPIAchievement;
            }
            return true;
        });

        // Revenue Upsell
        var customrecord_revenue_upsellSearchObj = search.create({
            type: "customrecord_revenue_upsell",
            filters:[
                ["custrecord_kpi_achievement_rev", "isnotempty", ""],
                "AND",
                ["isinactive", "is", "F"]
            ],
            columns:[
                search.createColumn({ name: "custrecord_employee_rev", label: "Employee" }),
                search.createColumn({ name: "custrecord_year_rev", label: "Year" }),
                search.createColumn({ name: "custrecord_h1_h2_rev", label: "H1/H2" }),
                search.createColumn({ name: "custrecord_kpi_achievement_rev", label: "KPI Achievement %" })
            ]
        });
        var searchResultCount = customrecord_revenue_upsellSearchObj.runPaged().count;
        log.debug("customrecord_revenue_upsellSearchObj result count", searchResultCount);
        customrecord_revenue_upsellSearchObj.run().each(function (revenueResults) {
            var revenueEmployee = revenueResults.getValue('custrecord_employee_rev');
            var revenueYear = revenueResults.getValue('custrecord_year_rev');
            var revenueH1H2 = revenueResults.getValue('custrecord_h1_h2_rev');
            var revenueKPIAchievement = parseFloat(revenueResults.getValue('custrecord_kpi_achievement_rev'));
            var revenueWeightage = 0;
            if (csatResultsCount < 2) {
                revenueWeightage = parseFloat(75 / 100);
            } else {
                revenueWeightage = parseFloat(50 / 100);
            }
            var revenueKPIWeightage = parseFloat(revenueKPIAchievement / 100);
            var finalRevenueWeightage = parseFloat(revenueWeightage * revenueKPIWeightage);
            var revYear = year[revenueYear];
            var revKey = `${revenueEmployee}-${revenueH1H2}-${revYear}`;
            if (!bonusCalculationData[revKey]) {
                bonusCalculationData[revKey] = {
                    employee: revenueEmployee,
                    h1h2: revenueH1H2,
                    year: revYear,
                    churn: 0,
                    csat: 0,
                    upsell: finalRevenueWeightage,
                    revenueKpi: revenueKPIAchievement,
                    total: 0
                }
            }
            else {
                bonusCalculationData[revKey].upsell = finalRevenueWeightage;
                bonusCalculationData[revKey].revenueKpi = revenueKPIAchievement;
            }
            return true;
        });

        log.debug("Final bonusCalculationData", bonusCalculationData);
        return Object.values(bonusCalculationData);
    }

    function map(context) {
        var data = JSON.parse(context.value);
        log.debug("Processing", data);

        context.write({
            key: data.employee + "-" + data.h1h2 + "-" + data.year,
            value: data
        });
    }

    function reduce(context) {
        try {
            var bonusYear = {
                "2020": 1, "2021": 2, "2022": 3, "2023": 4, "2024": 5, "2025": 6, "2026": 7, "2027": 8, "2028": 9, "2029": 10, "2030": 11,
                "2031": 12, "2032": 13, "2033": 14, "2034": 15, "2035": 16, "2036": 17, "2037": 18, "2038": 19, "2039": 20, "2040": 21
            };

            context.values.forEach(function (value) {
                var data = JSON.parse(value);
                log.debug("Final Processing", data);

                if (!data.employee || !data.year || !bonusYear[data.year] || !data.h1h2) {
                    log.error("Missing Required Fields", "Employee, valid year, or H1/H2 value is missing");
                    return;
                }

                var searchObj = search.create({
                    type: "customrecord_bonus_calculation",
                    filters: [
                        ["custrecord_employee_bonus", "anyof", data.employee],
                        "AND",
                        ["custrecord_year_bonus", "is", bonusYear[data.year]],
                        "AND",
                        ["custrecord_h1_h2_bonus", "is", data.h1h2]
                    ],
                    columns: [search.createColumn({ name: "internalid" })]
                });

                if (searchObj.runPaged().count === 0) {
                    try {
                        var bonusCalRec = record.create({ type: 'customrecord_bonus_calculation', isDynamic: true });
                    } catch (e) {
                        log.error("Record Creation Failed", "Error creating record: " + e.message);
                    }
                } else {
                    var bonusCalRec = record.load({
                        type: 'customrecord_bonus_calculation',
                        id: searchObj.run().getRange({ start: 0, end: 1 })[0].getValue('internalid'),
                        isDynamic: true
                    });
                    log.debug("Record Exists", "Updating - record already exists for employee " + data.employee + ", year " + data.year + ", period " + data.h1h2);
                }
                bonusCalRec.setValue({ fieldId: 'custrecord_employee_bonus', value: data.employee });
                bonusCalRec.setValue({ fieldId: 'custrecord_year_bonus', value: bonusYear[data.year] });
                bonusCalRec.setValue({ fieldId: 'custrecord_h1_h2_bonus', value: data.h1h2 });

                if (data.churnKpi !== undefined) {
                    var churnkpi = bonusCalRec.setValue({ fieldId: 'custrecord_customer_churn_percent_bonus', value: data.churnKpi + '%' });
                    log.debug("Churn KPI", "Churn KPI set to: " + churnkpi + '%');
                } else {
                    bonusCalRec.setValue({ fieldId: 'custrecord_customer_churn_percent_bonus', value: '0%' });
                }
                
                if (data.csatKpi !== undefined) {
                    bonusCalRec.setValue({ fieldId: 'custrecord_csat_score_percent', value: data.csatKpi + '%' });
                }else {
                    bonusCalRec.setValue({ fieldId: 'custrecord_csat_score_percent', value: '0%' });
                }
                
                if (data.revenueKpi !== undefined) {
                    bonusCalRec.setValue({ fieldId: 'custrecord_upsell_percent', value: data.revenueKpi + '%' });
                }else{
                    bonusCalRec.setValue({ fieldId: 'custrecord_upsell_percent', value: '0%' });
                }

                if (data.churn !== undefined && data.csat !== undefined && data.upsell !== undefined) {
                    var calculatedAvg = (parseFloat(data.churn) + parseFloat(data.csat) + parseFloat(data.upsell)) * 100;
                    bonusCalRec.setValue({ fieldId: 'custrecord_weighted_average_percent', value: calculatedAvg.toFixed(2) + '%' });
                }else {
                    bonusCalRec.setValue({ fieldId: 'custrecord_weighted_average_percent', value: '0%' });
                }

                var recordId = bonusCalRec.save({ enableSourcing: true, ignoreMandatoryFields: false });
                log.audit("Record Created/ Updated", "Bbonus calculation record created/ Updated with ID: " + recordId);
            });
        } catch (e) {
            log.error("Reduce Function Error", "Unexpected error in reduce function: " + e.message);
        }
    }

    function summarize(summary) {
        log.audit("Summary", "Map/Reduce Completed");
    }

    return {
        getInputData: getInputData,
        map: map,
        reduce: reduce,
        summarize: summarize
    };
});