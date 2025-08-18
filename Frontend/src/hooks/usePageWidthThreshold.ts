import { useEffect, useState } from "react";

export const usePageWidthThreshold = (threshold: number) => {
  const [aboveThreshold, setAboveThreshold] = useState(
    window.innerWidth > threshold
  );
  console.log("init");

  useEffect(() => {
    const handleResize = () => {
      const newAboveThreshold = window.innerWidth > threshold;
      if (newAboveThreshold !== aboveThreshold) {
        console.log(
          `${window.innerWidth} > ${threshold} = ${newAboveThreshold} ==? ${aboveThreshold}`
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
