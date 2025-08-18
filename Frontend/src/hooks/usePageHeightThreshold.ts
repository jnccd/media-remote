import { useEffect, useState } from "react";

export const usePageHeightThreshold = (threshold: number) => {
  const [aboveThreshold, setAboveThreshold] = useState(
    window.innerHeight > threshold
  );
  console.log("init");

  useEffect(() => {
    const handleResize = () => {
      const newAboveThreshold = window.innerHeight > threshold;
      if (newAboveThreshold !== aboveThreshold) {
        console.log(
          `${window.innerHeight} > ${threshold} = ${newAboveThreshold} ==? ${aboveThreshold}`
        );
        setAboveThreshold(newAboveThreshold);
      }
    };
    window.addEventListener("resize", handleResize);
    return () => {
      window.removeEventListener("resize", handleResize);
    };
  }, [aboveThreshold, setAboveThreshold]);

  return aboveThreshold;
};
