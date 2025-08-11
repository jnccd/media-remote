import axios from "axios";
import { ApiClient } from "../generated/apiClient";

export const baseUrl = import.meta.env.VITE_DEV_BACKEND_ADDRESS
  ? import.meta.env.VITE_DEV_BACKEND_ADDRESS
  : window.location.href;
export const axiosClient = axios.create({
  withCredentials: false,
});

export const mediaControlApiClient = new ApiClient(baseUrl, axiosClient);
